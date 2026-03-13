using sfTracker.Audio;
using sfTracker.Common;
using sfTracker.GUI;
using sfTracker.Helpers;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace sfTracker
{
    public partial class MainWindow : Window
    {
        private SynthEngine Engine;
        private readonly Stopwatch playbackClock = new();
        private readonly MainViewModel vm;
        private readonly double MousePositionOffsetX = 40;
        private readonly double MousePositionOffsetY = 450;

        private int totalRowCount = 0;
        private double resumePlaybackRow = 0;
        private double lastHorizontalScrollValue;
        private List<PatternBoundary> patternBoundaries = [];
        private bool IsClickingCell = false;
        private bool IsInternalScrollChange = false;
        public bool IsPlaying = false;
        public double currentRowPosition;
        public int currentlyPlayingNote;
        private string ProjectTitle = "";

        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
            vm.GetColumns(ProgramConstants.DefaultChannelCount, Tracker.ColumnWidth);
            DataContext = vm;

            InitialiseTracker(
                soundFont: ProgramConstants.DefaultSoundFont,
                patterns: [
                    new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount),
                ],
                BPM: vm.BPM
            );

            //this.KeyDown += new System.Windows.Input.KeyEventHandler(OnKeyPress);
            //this.KeyUp += new System.Windows.Input.KeyEventHandler(OnKeyRelease);
        }

        private void LoadSoundFont(string path)
        {
            ObservableCollection<SoundFontPreset> presets = vm.LoadSoundFont(path);
            Tracker.PresetList = presets;
        }

        private void InitialiseTracker(string soundFont, List<Pattern> patterns, int BPM)
        {
            Engine?.ResetTracker(0);
            Engine?.Dispose();
            DisableEventListeners();

            Engine = new SynthEngine(soundFont);
            Engine.Tracker.Patterns = patterns;
            Engine.Tracker.ActiveVoices = new Voice[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.CurrentVolumes = new int[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.TargetVolumes = new int[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.CurrentPannings = new PanEffect[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.TargetPannings = new PanEffect[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.ChannelMuteStatuses = new bool[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.EarlyStoppingIndex = vm.RowCount;
            Engine.Tracker.SetBPM(BPM);
            
            Tracker.ChannelCount = ProgramConstants.DefaultChannelCount;
            Tracker.RowsPerPattern = vm.RowCount;
            Tracker.MuteButtonStartPositionsX = new double[ProgramConstants.DefaultChannelCount];
            Tracker.SoloButtonStartPositionsX = new double[ProgramConstants.DefaultChannelCount];
            Tracker.ChannelStatuses = vm.Columns;
            Tracker.RowWidth = ProgramConstants.DefaultChannelCount * Tracker.ColumnWidth;
            Tracker.Patterns = Engine.Tracker.Patterns;
            Tracker.Engine = Engine;
            Tracker.RowHighlight = vm.RowHighlight;
            Tracker.Focus();

            // reset frame select if fewer patterns than max visible frames 
            if (Tracker.Patterns.Count <= Tracker.MaxVisibleFrames)
            {
                Tracker.FirstVisibleFrame = 0;
                Tracker.LastVisibleFrame = Tracker.Patterns.Count;
            }

            ComputePatternBoundaries();
            LoadSoundFont(soundFont);
            EnableEventListeners();
            SelectedSoundFont.Text = $"{soundFont}";

            // set scrollbar maximum values
            VerticalScrollBar.Maximum = totalRowCount - 1;
            HorizontalScrollBar.Maximum = Tracker.ChannelCount * Tracker.FieldsPerChannel - 1;
            FrameVerticalScrollBar.Maximum = Tracker.Patterns.Count - 1;
            FrameVerticalScrollBar.Visibility = Tracker.Patterns.Count > 1 ? Visibility.Visible : Visibility.Hidden; // hide scrollbar if only 1 pattern
            RemovePatternButton.IsEnabled = Tracker.Patterns.Count > 1; // disable delete pattern button if only 1 pattern exists
        }

        private void DisableEventListeners()
        {
            CompositionTarget.Rendering -= (s, e) => Tracker.SetCurrentRow(Engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted -= StartPlayback;
            Tracker.VerticalScrollbarValueChanged -= SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged -= SetHorizontalScrollbarValue;
            Tracker.FrameVerticalScrollbarValueChanged -= SetFrameVerticalScrollbarValue;
            Tracker.RowChanged -= Tracker_RowChanged;
            Tracker.ColumnChanged -= Tracker_ColumnChanged;
            Tracker.PatternChanged -= Tracker_PatternChanged;
            vm.PropertyChanged -= InstrumentChanged;
        }

        private void EnableEventListeners()
        {
            CompositionTarget.Rendering += (s, e) => Tracker.SetCurrentRow(Engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted += StartPlayback;
            Tracker.VerticalScrollbarValueChanged += SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged += SetHorizontalScrollbarValue;
            Tracker.FrameVerticalScrollbarValueChanged += SetFrameVerticalScrollbarValue;
            Tracker.RowChanged += Tracker_RowChanged;
            Tracker.ColumnChanged += Tracker_ColumnChanged;
            Tracker.PatternChanged += Tracker_PatternChanged;
            vm.PropertyChanged += InstrumentChanged;
        }

        private void InstrumentChanged(object sender, PropertyChangedEventArgs e)
        {
            if (vm.SelectedPreset != null)
            {
                Tracker.SelectInstrument(vm.SelectedPreset.Instrument, vm.SelectedPreset.ID);
                Tracker.SelectBank(vm.SelectedPreset.Bank);
            }
        }
        private void OnFrame(object sender, EventArgs e)
        {
            double time = playbackClock.Elapsed.TotalSeconds;
            double rowDuration = (60.0 / (Engine.Tracker.BPM * Engine.Tracker.TicksPerBeat)) * Engine.Tracker.Speed;
            double rowsAdvanced = Math.Floor(time / rowDuration);

            Tracker.CurrentRowPosition = (rowsAdvanced + resumePlaybackRow) % totalRowCount;
            if (Tracker.CurrentRowPosition == 0) { Tracker.ResetToFirstRow(); } // need this to prevent frames scrolling back after loop ends
            Tracker.GlobalCurrentRow = (int)Tracker.CurrentRowPosition;
            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);
        }

        private void ComputePatternBoundaries()
        {
            int currentRow = 0;
            patternBoundaries.Clear();

            for (int i = 0; i < Tracker.Patterns.Count; i++)
            {
                var pattern = Tracker.Patterns[i];

                patternBoundaries.Add(new PatternBoundary
                {
                    Index = i,
                    StartingRow = currentRow,
                    RowCount = Tracker.RowsPerPattern
                });

                currentRow += Tracker.RowsPerPattern;
            }

            totalRowCount = currentRow;
            Tracker.TotalRowCount = totalRowCount;
        }

        public void StartPlayback(int currentPattern)
        {
            Engine.Start(currentPattern);

            int currentRow = Tracker.GlobalCurrentRow;

            foreach (var patternBoundary in patternBoundaries)
            {
                if (
                    currentRow >= patternBoundary.StartingRow &&
                    currentRow < patternBoundary.StartingRow + patternBoundary.RowCount
                )
                {
                    resumePlaybackRow = patternBoundary.StartingRow;
                    break;
                }
            }

            if (!IsPlaying)
            {
                IsPlaying = true;
                HandleSettingsButtonActivity(isEnabled: false);
                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
                HandleSettingsButtonActivity(isEnabled: true);
                playbackClock.Stop();
                CompositionTarget.Rendering -= OnFrame;
            }

            InvalidateVisual();
        }

        public void SetVerticalScrollbarValue(double value)
        {
            VerticalScrollBar.Value = value;
            Tracker.EnsureVisible();
        }

        public void SetHorizontalScrollbarValue(double value)
        {
            HorizontalScrollBar.Value = value;
        }

        public void SetFrameVerticalScrollbarValue(double value)
        {
            FrameVerticalScrollBar.Value = value;
        }

        private void UpdateVisibleFrames(double value)
        {
            if (Tracker.Patterns.Count > Tracker.MaxVisibleFrames) // if more patterns than max allowed to display
            {
                if (value < Tracker.MaxVisibleFrames) // if scroll value is less than max visible frames, reset them
                {
                    Tracker.FirstVisibleFrame = 0;
                    Tracker.LastVisibleFrame = Tracker.MaxVisibleFrames;
                    return;
                }

                // update first and last visible frames based on scroll value
                if (value == Tracker.LastVisibleFrame)
                {
                    Tracker.FirstVisibleFrame++;
                    Tracker.LastVisibleFrame++;
                }
                else
                {
                    Tracker.FirstVisibleFrame--;
                    Tracker.LastVisibleFrame--;
                }
            }
            else
            {
                Tracker.LastVisibleFrame = Tracker.Patterns.Count; // last visible frame defaults to pattern number
            }
        }

        private void Tracker_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // prevent scroll event firing if mouse not in tracker region
            Grid trackerSectionGrid = (Grid)sender;
            Point pos = e.GetPosition(trackerSectionGrid);
            if (IsPlaying || !CheckCursorInsideGrid(trackerSectionGrid, pos)) { return; }

            double change = e.Delta;

            if (change < 0)
                SetVerticalScrollbarValue(VerticalScrollBar.Value + VerticalScrollBar.SmallChange);
            else if (change > 0)
                SetVerticalScrollbarValue(VerticalScrollBar.Value - VerticalScrollBar.SmallChange);
        }

        private void FrameSelect_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // prevent scroll event firing if mouse not in tracker region
            Grid frameSelectGrid = (Grid)sender;
            Point pos = e.GetPosition(frameSelectGrid);
            if (IsPlaying || !CheckCursorInsideGrid(frameSelectGrid, pos)) { return; }

            double change = e.Delta;

            if (change < 0)
                SetFrameVerticalScrollbarValue(FrameVerticalScrollBar.Value + FrameVerticalScrollBar.SmallChange);
            else if (change > 0)
                SetFrameVerticalScrollbarValue(FrameVerticalScrollBar.Value - FrameVerticalScrollBar.SmallChange);
        }

        private void Tracker_RowChanged(int newRow)
        {
            IsInternalScrollChange = true;
            VerticalScrollBar.Value = newRow;
            IsInternalScrollChange = false;
        }

        private void Tracker_ColumnChanged(int newColumn)
        {
            IsInternalScrollChange = true;
            HorizontalScrollBar.Value = newColumn;
            IsInternalScrollChange = false;
        }

        private void Tracker_PatternChanged(int newPattern)
        {
            IsInternalScrollChange = true;
            FrameVerticalScrollBar.Value = newPattern;
            UpdateVisibleFrames(newPattern);
            IsInternalScrollChange = false;
        }

        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Tracker.GlobalCurrentRow = (int)e.NewValue;
            Tracker.Focus();
        }

        private void HorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // TODO: clean this up, not really working that well
            if (!IsClickingCell)
            {
                double delta = e.NewValue - lastHorizontalScrollValue;

                if (Math.Abs(delta) > 1)
                {
                    HorizontalScrollBar.Value = lastHorizontalScrollValue + Math.Sign(delta);
                    return;
                }

                lastHorizontalScrollValue = HorizontalScrollBar.Value;
                Tracker.GlobalCurrentColumn = (int)HorizontalScrollBar.Value;
                Tracker.Focus();
            }
        }

        private void FrameVerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Tracker.GlobalCurrentRow = (int)e.NewValue * vm.RowCount;
        }

        private void Tracker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsPlaying) { return; }
            double mousePosX = e.GetPosition(this).X;
            double mousePosY = e.GetPosition(this).Y;

            double absoluteX = mousePosX - MousePositionOffsetX;
            double absoluteY = mousePosY - (MousePositionOffsetY - (Tracker.RowHeight * Tracker.FirstVisibleRow));

            if (
                absoluteX < 0 || absoluteX > Tracker.ColumnWidth * Tracker.ChannelCount ||
                absoluteY < 0 || absoluteY > Tracker.RowHeight * totalRowCount
            ) { return; }

            GetCellFromCoord(absoluteX, absoluteY);
            Tracker.Focus();
        }

        private void GetCellFromCoord(double x, double y)
        {
            double channelWidth = Tracker.ColumnWidth;
            for (int channel = 0; channel < Tracker.ChannelCount; channel++)
            {
                double startingPosX = channel * channelWidth;
                
                ColumnDefinitions cols = new ColumnDefinitions(
                    startingPosX, Tracker.NoteWidth, Tracker.DigitWidth, Tracker.ChannelInnerPadding
                );
                List<double> colXCoords = cols.GetColumnCoordinates();
                List<double> colXCWidths = cols.GetColumnWidths();

                for (int i = 0; i < colXCoords.Count; i++)
                {
                    if (x >= colXCoords[i] && x <= colXCoords[i] + colXCWidths[i])
                    {
                        int cellColumnToSelect = channel * Tracker.FieldsPerChannel + i;
                        IsClickingCell = true;
                        Tracker.CurrentChannel = channel;
                        Tracker.GlobalCurrentColumn = cellColumnToSelect;
                        lastHorizontalScrollValue = cellColumnToSelect;
                        Tracker.CurrentField = (TrackerField)i;
                        IsClickingCell = false;
                        break;
                    }
                }
            }

            int cellRowToSelect = (int)Math.Floor(y / Tracker.RowHeight);
            IsClickingCell = true;
            Tracker.GlobalCurrentRow = cellRowToSelect;
            IsClickingCell = false;
        }

        // https://youtu.be/Heq8qve1Vts
        private void Button_OpenSF2(object sender, RoutedEventArgs e)
        {
            string fileName = GetOpenFileDialog(title: "Select.sf2 file", filter: "SoundFont Files (*.sf2)|*.sf2");
            if (fileName == "") { return; }
            string soundFontName = GetParsedFileName(fileName);
            InitialiseTracker(soundFontName, Engine.Tracker.Patterns, ProgramConstants.DefaultBPM);
        }

        private static bool CheckCursorInsideGrid(Grid grid, Point pos)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X > grid.ActualWidth || pos.Y > grid.ActualHeight)
                return false;
            return true;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            double mousePosX = e.GetPosition(this).X;
            double mousePosY = e.GetPosition(this).Y;

            double absoluteX = mousePosX - MousePositionOffsetX;
            double absoluteY = mousePosY - MousePositionOffsetY;

            // handle click of channel settings section (mute/solo)
            HandleChannelSettingsChange(absoluteX, absoluteY);

            // update GUI
            InvalidateVisual();
        }

        private void HandleChannelSettingsChange(double x, double y)
        {
            // check if click is within the buttons' y range
            double yLowerBound = Tracker.ChannelButtonStartPositionY;
            double yUpperBound = Tracker.ChannelButtonStartPositionY + Tracker.ChannelButtonSize;
            if (y < yLowerBound || y > yUpperBound) { return; }

            // for the mute button, mute or unmute channel accordingly
            int channelToMute =
                ChannelSettings.HandleChannelButtonClick(
                    x,
                    Tracker.MuteButtonStartPositionsX,
                    Tracker.ChannelCount,
                    Tracker.ChannelButtonSize
            );
            if (channelToMute != -1 && vm.Columns[channelToMute].IsMuted)
                ChannelSettings.HandleUnmute(channelToMute, vm, Engine, Tracker.ChannelStatuses);
            else
                ChannelSettings.HandleMute(channelToMute, vm, Engine, Tracker.ChannelStatuses);

            // for the solo button, handle solo accordingly
            int channelToSolo =
                ChannelSettings.HandleChannelButtonClick(
                    x,
                    Tracker.SoloButtonStartPositionsX,
                    Tracker.ChannelCount,
                    Tracker.ChannelButtonSize
                );
            ChannelSettings.HandleSolo(channelToSolo, vm, Engine, Tracker.ChannelStatuses, Tracker.ChannelCount);
        }

        private void ListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z) // prevent key presses moving focus in the preset select window
                e.Handled = true;
        }

        private void AddPatternButton_Click(object sender, RoutedEventArgs e)
        {
            Tracker.Patterns.Insert(
                Tracker.currentPatternIndex + 1, 
                new Pattern(
                    rowCount: ProgramConstants.MaxRowCount,
                    channels: ProgramConstants.DefaultChannelCount
                )
            );
            FrameVerticalScrollBar.Value += FrameVerticalScrollBar.SmallChange;
            InitialiseTracker(SelectedSoundFont.Text, (List<Pattern>)Tracker.Patterns, vm.BPM);
        }

        private void RemovePatternButton_Click(object sender, RoutedEventArgs e)
        {
            if (Tracker.Patterns.Count == 1) { return; } // don't allow removal when only one pattern exists
            if (Tracker.currentPatternIndex == Tracker.Patterns.Count - 1) { FrameVerticalScrollBar.Value -= FrameVerticalScrollBar.SmallChange; }
            Tracker.Patterns.RemoveAt(Tracker.currentPatternIndex); // TODO: consider making it more clear that current pattern is being deleted
            InitialiseTracker(SelectedSoundFont.Text, (List<Pattern>)Tracker.Patterns, ProgramConstants.DefaultBPM);
        }

        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            Tracker.Focus();
            UpdateTrackerSettings();
        }

        private void SettingsArrow_Click(object sender, RoutedEventArgs e)
        {
            RepeatButton button = (RepeatButton)sender;
            string tag = button.Tag.ToString();

            int step = 1;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                step = 5;

            switch (tag)
            {
                case "BPM_Up":
                    vm.BPM += step;
                    break;

                case "BPM_Down":
                    vm.BPM -= step;
                    break;

                case "Speed_Up":
                    vm.Speed += step;
                    break;

                case "Speed_Down":
                    vm.Speed -= step;
                    break;

                case "RowCount_Up":
                    vm.RowCount += step;
                    break;

                case "RowCount_Down":
                    vm.RowCount -= step;
                    break;

                case "RowHighlight_Up":
                    vm.RowHighlight += step;
                    break;

                case "RowHighlight_Down":
                    vm.RowHighlight -= step;
                    break;
            }

            Tracker.Focus();
            UpdateTrackerSettings();
        }

        private void UpdateTrackerSettings()
        {
            if (vm.BPM != Engine.Tracker.BPM)
            {
                Engine.Tracker.SetBPM(vm.BPM);
            }
            else if (vm.Speed != Engine.Tracker.Speed)
            {
                Engine.Tracker.SetSpeed(vm.Speed);
            }
            else if (vm.RowCount != Tracker.RowsPerPattern)
            {
                Engine.Tracker.EarlyStoppingIndex = vm.RowCount;
                Tracker.GlobalCurrentRow = 0; // reset to start to avoid indexing issues
                Tracker.RowsPerPattern = vm.RowCount;
                Tracker.ResetToFirstRow();
                InitialiseTracker(SelectedSoundFont.Text, Engine.Tracker.Patterns, vm.BPM);
            }
            else if (vm.RowHighlight != Tracker.RowHighlight)
            {
                Tracker.RowHighlight = vm.RowHighlight;
            }
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
                Tracker.Focus();
                UpdateTrackerSettings();
            }
        }

        private void HandleSettingsButtonActivity(bool isEnabled)
        {
            AddPatternButton.IsEnabled = isEnabled;
            RemovePatternButton.IsEnabled = Tracker.Patterns.Count > 1 && isEnabled;

            BPMTextBox.IsReadOnly = !isEnabled;
            BPMTextBox.Focusable = isEnabled;
            BPM_Up_Button.IsEnabled = isEnabled;
            BPM_Down_Button.IsEnabled = isEnabled;

            SpeedTextBox.IsReadOnly = !isEnabled;
            SpeedTextBox.Focusable = isEnabled;
            Speed_Up_Button.IsEnabled = isEnabled;
            Speed_Down_Button.IsEnabled = isEnabled;

            RowCountTextBox.IsReadOnly = !isEnabled;
            RowCountTextBox.Focusable = isEnabled;
            RowCount_Up_Button.IsEnabled = isEnabled;
            RowCount_Down_Button.IsEnabled = isEnabled;

            RowHighlightTextBox.IsReadOnly = !isEnabled;
            RowHighlightTextBox.Focusable = isEnabled;
            RowHighlight_Up_Button.IsEnabled = isEnabled;
            RowHighlight_Down_Button.IsEnabled = isEnabled;

            CreateNew_Button.IsEnabled = isEnabled;
            Save_Button.IsEnabled = isEnabled;
            SaveAs_Button.IsEnabled = isEnabled;
            Load_Button.IsEnabled = isEnabled;

            OpenSF2_Button.IsEnabled = isEnabled;
        }

        private void CreateNewButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = GetConfirmationDialog("Create New Project");
            if (!confirmed) { return; }

            vm.SetViewModelData(
                ProgramConstants.DefaultBPM,
                ProgramConstants.DefaultSpeed,
                ProgramConstants.DefaultRowCount,
                ProgramConstants.DefaultRowHighlight,
                ""
            );
            Tracker.GlobalCurrentRow = 0;
            //Tracker.ResetToFirstRow();

            ProjectTitle = "";
            vm.ResetColumns();
            InitialiseTracker(
                soundFont: ProgramConstants.DefaultSoundFont,
                patterns: [
                    new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount),
                ],
                BPM: ProgramConstants.DefaultBPM
            );
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = (System.Windows.Controls.Button)sender;
            string tag = button.Tag.ToString();
            SaveProject(ProjectTitle, isNewProject: tag == "Save_As");
        }
        
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = GetConfirmationDialog("Load Project");
            if (!confirmed) { return; }

            string fileName = GetOpenFileDialog(title: "Select .sft file", filter: "sfTracker Files (*.sft)|*.sft");
            if (fileName == "") { return; }

            LoadProject(GetParsedFileName(fileName));
        }

        private void SaveProject(string filePath, bool isNewProject = false)
        {
            if (isNewProject || filePath == "")
            {
                string fileName = GetSaveFileDialog(title: "Save project", filter: "sfTracker Files (*.sft)|*.sft");
                if (fileName == "") { return; }
                filePath = GetParsedFileName(fileName);
                ProjectTitle = filePath;
                vm.WindowTitle = filePath;
            }

            // TODO: include channel count (if i'm making them generic)
            List<Pattern> resizedPatterns = [];
            foreach (var pattern in Engine.Tracker.Patterns)
            {
                Pattern resizedPattern = new Pattern(rowCount: vm.RowCount, channels: ProgramConstants.DefaultChannelCount);
                for (int i = 0; i < vm.RowCount; i++)
                    resizedPattern.Rows[i] = pattern.Rows[i];

                resizedPatterns.Add(resizedPattern);
            }

            ProjectFile project = new()
            {
                ProjectName = filePath,
                SoundFont = SelectedSoundFont.Text,
                BPM = vm.BPM,
                Speed = vm.Speed,
                RowCount = vm.RowCount,
                RowHighlight = vm.RowHighlight,
                Patterns = resizedPatterns
            };

            JsonSerializerOptions jsonOptions = new()
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(project, jsonOptions);
            File.WriteAllText(filePath, json);
        }

        private void LoadProject(string filePath)
        {
            string json = File.ReadAllText(filePath);
            ProjectFile project = JsonSerializer.Deserialize<ProjectFile>(json);

            vm.SetViewModelData(project.BPM, project.Speed, project.RowCount, project.RowHighlight, project.ProjectName);

            List<Pattern> resizedPatterns = [];

            foreach (var pattern in project.Patterns)
            {
                Pattern filledPattern = new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount);
                for (int i = 0; i < project.RowCount; i++)
                    filledPattern.Rows[i] = pattern.Rows[i];

                resizedPatterns.Add(filledPattern);
            }

            ProjectTitle = filePath;
            vm.ResetColumns();
            InitialiseTracker(project.SoundFont, resizedPatterns, project.BPM);
            UpdateTrackerSettings();
        }

        private bool GetConfirmationDialog(string title)
        {
            ConfirmDialog confirmDialog = new ConfirmDialog
            {
                Owner = this,
                Title = title
            };

            confirmDialog.ShowDialog();
            if (!confirmDialog.Confirmed) { return false; }
            return true;
        }

        private static string GetSaveFileDialog(string title, string filter)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
            };

            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
                return dialog.FileName;

            return "";
        }

        private static string GetParsedFileName(string fileName)
        {
            return fileName[(fileName.LastIndexOf('\\') + 1)..];
        }

        private static string GetOpenFileDialog(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false
            };

            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
                return dialog.FileName;

            return "";
        }

        //private void UpdatePatternsRowCount()
        //{
        //    // TODO: maybe find a way to allow removal of rows without deleting the data
        //    // this currently just overwrites any data which is not within the range and it's gone forever
        //    for (int i = 0; i < Tracker.Patterns.Count; i++)
        //    {
        //        Pattern oldPattern = Tracker.Patterns[i];
        //        Pattern newPattern = new Pattern(rowCount: vm.RowCount, channels: defaultChannelCount);
        //        for (int j = 0; j < vm.RowCount; j++)
        //        {
        //            if (j == oldPattern.Rows.Length) { break; }
        //            newPattern.Rows[j] = oldPattern.Rows[j];
        //        }

        //        Tracker.GlobalCurrentRow = 0; // reset to start to avoid indexing issues
        //        Engine.Tracker.Patterns[i] = newPattern;
        //    }

        //    InitialiseTracker(SelectedSoundFont.Text, Engine.Tracker.Patterns, vm.BPM);
        //}

        //TODO: try to get this to work properly if i have time

        //private void OnKeyPress(object sender, System.Windows.Input.KeyEventArgs e)
        //{
        //    Console.WriteLine($"note: {Tracker.GetMidiNote(e.Key)}");
        //    if (Tracker.GetMidiNote(e.Key) == null) { return; }

        //    if ((int)Tracker.GetMidiNote(e.Key) == currentlyPlayingNote ) { return; } 

        //    if (e.Key != Key.Enter)
        //    {
        //        Console.WriteLine("ojisdfgjiogsd");
        //        Engine.Start();
        //        Engine.Tracker.TriggerNoteWithKeyboard(
        //            Tracker.CurrentColumn,
        //            (int)Tracker.GetMidiNote(e.Key),
        //            vm.SelectedPreset.Bank,
        //            vm.SelectedPreset.Instrument,
        //            100,
        //            trigger: true
        //        );

        //        currentlyPlayingNote = (int)Tracker.GetMidiNote(e.Key);
        //    }
        //}

        //private void OnKeyRelease(object sender, System.Windows.Input.KeyEventArgs e)
        //{
        //    Engine.StopAudio();
        //    Engine.Tracker.TriggerNoteWithKeyboard(
        //        Tracker.CurrentColumn,
        //        currentlyPlayingNote,
        //        -1,
        //        -1,
        //        100
        //    );
        //    currentlyPlayingNote = -1;
        //}
    }
}