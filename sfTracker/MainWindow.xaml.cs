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
    /// <summary>
    /// Class representing the main window of the WPF application. 
    /// </summary>
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
        private List<PatternBoundary> PatternBoundaries = [];
        private bool IsClickingCell = false;
        private bool IsInternalScrollChange = false;
        public bool IsPlaying = false;
        public double currentRowPosition;
        public int currentlyPlayingNote;
        private string ProjectTitle = "";

        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel(); // initialise view model for updating fields in the GUI
            vm.GetColumns(ProgramConstants.DefaultChannelCount, Tracker.ColumnWidth); // get column data for mute/solo statuses
            DataContext = vm; // set the data context

            // initialise tracker with default settings
            // TODO: look into way of starting with no soundfont
            InitialiseTracker(
                soundFont: ProgramConstants.DefaultSoundFont,
                patterns: [
                    new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount),
                ],
                bpm: vm.BPM
            );

            //this.KeyDown += new System.Windows.Input.KeyEventHandler(OnKeyPress);
            //this.KeyUp += new System.Windows.Input.KeyEventHandler(OnKeyRelease);
        }

        /// <summary>
        /// Method to initialise the tracker and GUI.
        /// </summary>
        private void InitialiseTracker(string soundFont, List<Pattern> patterns, int bpm)
        {
            // reset tracker engine if one exists already
            // also disable event listeners before changing settings
            Engine?.ResetTracker(0);
            Engine?.Dispose();
            DisableEventListeners();

            InitialiseEngine(soundFont, patterns, bpm); // instantiate the engine, setting all required fields to default settings
            InitialiseTrackerGrid(); // set required tracker GUI data

            ComputePatternBoundaries(); // determine start and end indeces for each pattern
            LoadSoundFont(soundFont); // load the soundfont
            EnableEventListeners();
            SelectedSoundFont.Text = $"{soundFont}"; // update soundfont display TODO: move this to view model

            // set scrollbar boundaries
            UpdateScrollbarBounds();
            RemovePatternButton.IsEnabled = Tracker.Patterns.Count > 1; // disable delete pattern button if only 1 pattern exists
            Tracker.Focus(); // direct focus to tracker grid
        }

        /// <summary>
        /// Method to initialise the tracker engine.
        /// </summary>
        private void InitialiseEngine(string soundFont, List<Pattern> patterns, int bpm)
        {
            Engine = new SynthEngine(soundFont);
            Engine.Tracker.Patterns = patterns;
            Engine.Tracker.ActiveVoices = new Voice[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.CurrentVolumes = new int[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.TargetVolumes = new int[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.CurrentPannings = new PanEffect[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.TargetPannings = new PanEffect[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.ChannelMuteStatuses = new bool[ProgramConstants.DefaultChannelCount];
            Engine.Tracker.EarlyStoppingIndex = vm.RowCount;
            Engine.Tracker.SetBPM(bpm);
        }

        /// <summary>
        /// Method to initialise the tracker grid (GUI).
        /// </summary>
        private void InitialiseTrackerGrid()
        {
            Tracker.Engine = Engine;
            Tracker.ChannelCount = ProgramConstants.DefaultChannelCount;
            Tracker.Patterns = Engine.Tracker.Patterns;

            //Tracker.RowWidth = ProgramConstants.DefaultChannelCount * Tracker.ColumnWidth;
            Tracker.RowsPerPattern = vm.RowCount;
            Tracker.RowHighlight = vm.RowHighlight;
            Tracker.ChannelStatuses = vm.Columns;
            
            // used for determining x-coordinates of mute/solo buttons in each tracker column
            Tracker.MuteButtonStartPositionsX = new double[ProgramConstants.DefaultChannelCount];
            Tracker.SoloButtonStartPositionsX = new double[ProgramConstants.DefaultChannelCount];

            // reset frame select if fewer patterns than max visible frames 
            if (Tracker.Patterns.Count <= Tracker.MaxVisibleFrames)
            {
                Tracker.FirstVisibleFrame = 0;
                Tracker.LastVisibleFrame = Tracker.Patterns.Count;
            }
        }

        /// <summary>
        /// Method to update scrollbar boundaries based on tracker data.
        /// </summary>
        private void UpdateScrollbarBounds()
        {
            VerticalScrollBar.Maximum = totalRowCount - 1; // tracker grid vertical scrollbar
            HorizontalScrollBar.Maximum = Tracker.ChannelCount * Tracker.FieldsPerChannel - 1; // tracker grid horizontal scrollbar
            FrameVerticalScrollBar.Maximum = Tracker.Patterns.Count - 1; // frame select vertical scrollbar
            FrameVerticalScrollBar.Visibility = Tracker.Patterns.Count > 1 ? Visibility.Visible : Visibility.Hidden; // hide scrollbar if only 1 pattern
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

        private void LoadSoundFont(string path)
        {
            ObservableCollection<SoundFontPreset> presets = vm.LoadSoundFont(path);
            Tracker.PresetList = presets;
        }

        /// <summary>
        /// Method to update selected instrument within the tracker grid's context.
        /// </summary>
        private void InstrumentChanged(object sender, PropertyChangedEventArgs e)
        {
            if (vm.SelectedPreset != null)
            {
                Tracker.SelectInstrument(vm.SelectedPreset.Instrument, vm.SelectedPreset.ID);
                Tracker.SelectBank(vm.SelectedPreset.Bank);
            }
        }

        /// <summary>
        /// Main rendering function. This is called each render frame to update the current row based on the BPM and elapsed time.
        /// The vertical scrollbar is also updated to scroll along as the tracker advances.
        /// </summary>
        private void OnFrame(object sender, EventArgs e)
        {
            double time = playbackClock.Elapsed.TotalSeconds;
            double rowDuration = (60.0 / (Engine.Tracker.BPM * Engine.Tracker.TicksPerBeat)) * Engine.Tracker.Speed;
            double rowsAdvanced = Math.Floor(time / rowDuration);

            // resumePlaybackRow is used to start playing back from the first row of current pattern
            Tracker.CurrentRowPosition = (rowsAdvanced + resumePlaybackRow) % totalRowCount;

            // this is needed to prevent buggy frame selection after a full loop ends
            if (Tracker.CurrentRowPosition == 0)
                Tracker.ResetToFirstRow();

            Tracker.GlobalCurrentRow = (int)Tracker.CurrentRowPosition;
            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);
        }

        /// <summary>
        /// Method to calculate where each pattern starts and ends, as well as determining the total row count.
        /// </summary>
        private void ComputePatternBoundaries()
        {
            PatternBoundaries.Clear(); // clear array before recalculating

            int currentRow = 0;
            for (int i = 0; i < Tracker.Patterns.Count; i++)
            {
                PatternBoundaries.Add(new PatternBoundary
                {
                    Index = i,
                    StartingRow = currentRow,
                    RowCount = Tracker.RowsPerPattern
                });

                // currently, each pattern is a fixed length
                // so the next pattern should start a fixed distance from the previous one
                currentRow += Tracker.RowsPerPattern;
            }

            totalRowCount = currentRow;
            Tracker.TotalRowCount = totalRowCount;
        }

        /// <summary>
        /// Method to start playback, called from the GUI when Enter is pressed.
        /// TODO: create buttons for playing/stopping.
        /// </summary>
        public void StartPlayback(int currentPattern)
        {
            Engine.Start(currentPattern);

            int currentRow = Tracker.GlobalCurrentRow;

            // determine resumePlaybackRow based on pattern boundaries
            foreach (var patternBoundary in PatternBoundaries)
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
                HandleButtonState(isEnabled: false); // disable buttons when playback is started
                playbackClock.Restart(); // restart the playback clock for correct timing
                CompositionTarget.Rendering += OnFrame; // start playback rendering
            }
            else
            {
                IsPlaying = false;
                HandleButtonState(isEnabled: true); // enable buttons again when playback is stopped
                playbackClock.Stop(); // stop the playback clock
                CompositionTarget.Rendering -= OnFrame; // stop playback rendering
            }

            InvalidateVisual(); // re-render GUI
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

        /// <summary>
        /// Method to update the frame select above the tracker to show at most 6 frames.
        /// </summary>
        private void UpdateVisibleFrames(double value)
        {
            if (Tracker.Patterns.Count > Tracker.MaxVisibleFrames)
            {
                // if scroll value is less than max visible frames, reset them
                if (value < Tracker.MaxVisibleFrames)
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

        /// <summary>
        /// Method to handle scroll events in the tracker grid.
        /// </summary>
        private void Tracker_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // prevent scroll event firing if mouse not in tracker grid region
            Grid trackerSectionGrid = (Grid)sender;
            Point pos = e.GetPosition(trackerSectionGrid);
            if (IsPlaying || !CheckCursorInsideGrid(trackerSectionGrid, pos)) { return; }

            double change = e.Delta;

            if (change < 0)
                SetVerticalScrollbarValue(VerticalScrollBar.Value + VerticalScrollBar.SmallChange);
            else if (change > 0)
                SetVerticalScrollbarValue(VerticalScrollBar.Value - VerticalScrollBar.SmallChange);
        }

        /// <summary>
        /// Method to handle scroll events in the frame select grid.
        /// </summary>
        private void FrameSelect_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // prevent scroll event firing if mouse not in frame select region
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
            UpdateVisibleFrames(newPattern); // update visible frames when switchin to a different pattern
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

        /// <summary>
        /// Method to handle click events in the tracker grid.
        /// </summary>
        private void Tracker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsPlaying) { return; }
            double mousePosX = e.GetPosition(this).X;
            double mousePosY = e.GetPosition(this).Y;

            double absoluteX = mousePosX - MousePositionOffsetX;
            double absoluteY = mousePosY - (MousePositionOffsetY - (Tracker.RowHeight * Tracker.FirstVisibleRow));

            // do nothing if click is outside the main tracker area
            if (
                absoluteX < 0 || absoluteX > Tracker.ColumnWidth * Tracker.ChannelCount ||
                absoluteY < 0 || absoluteY > Tracker.RowHeight * totalRowCount
            ) { return; }

            // get the selected cell from the click coordinates
            GetCellFromCoord(absoluteX, absoluteY);
            Tracker.Focus();
        }

        /// <summary>
        /// Method to get the tracker grid cell based on a given coordinate pair.
        /// </summary>
        private void GetCellFromCoord(double x, double y)
        {
            double channelWidth = Tracker.ColumnWidth;
            for (int channel = 0; channel < Tracker.ChannelCount; channel++)
            {
                double startingPosX = channel * channelWidth;
                
                ColumnDefinitions cols = new ColumnDefinitions(
                    startX:     startingPosX,
                    noteWidth:  Tracker.NoteWidth,
                    digitWidth: Tracker.DigitWidth,
                    padding:    Tracker.ChannelInnerPadding
                );
                
                // get x-coordinates and widths of fields for current column
                List<double> colXCoords = cols.GetColumnCoordinates();
                List<double> colXWidths = cols.GetColumnWidths();

                for (int i = 0; i < colXCoords.Count; i++)
                {
                    if (x >= colXCoords[i] && x <= colXCoords[i] + colXWidths[i])
                    {
                        // update tracker data based on selected column
                        // this essentially tells it which field has been selected
                        // IsClickingCell is used to ensure values are updated before breaking
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

            // update tracker data based on selected row
            int cellRowToSelect = (int)Math.Floor(y / Tracker.RowHeight);
            IsClickingCell = true;
            Tracker.GlobalCurrentRow = cellRowToSelect;
            IsClickingCell = false;
        }

        /// <summary>
        /// Method to handle click of the change soundfont button.
        /// https://youtu.be/Heq8qve1Vts
        /// </summary>
        private void OpenSF2Button_Click(object sender, RoutedEventArgs e)
        {
            string fileName = GetOpenFileDialog(title: "Select.sf2 file", filter: "SoundFont Files (*.sf2)|*.sf2");
            if (fileName == "") { return; }
            string soundFontName = GetParsedFileName(fileName);
            InitialiseTracker(soundFontName, Engine.Tracker.Patterns, ProgramConstants.DefaultBPM);
        }

        /// <summary>
        /// Method to check if a click's coordinate value is within a specific grid space.
        /// </summary>
        private static bool CheckCursorInsideGrid(Grid grid, Point pos)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X > grid.ActualWidth || pos.Y > grid.ActualHeight)
                return false;
            return true;
        }

        /// <summary>
        /// Method which fires when clicking anywhere on the screen.
        /// This is used specifically for the mute/solo buttons in each tracker column.
        /// </summary>
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

        /// <summary>
        /// Method to mute/solo channels based on coordinate of click.
        /// </summary>
        private void HandleChannelSettingsChange(double x, double y)
        {
            // check if click is within the buttons' y ranges
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

            // handle mute or unmute of channel
            if (channelToMute != -1 && vm.Columns[channelToMute].IsMuted)
                ChannelSettings.HandleUnmute(channelToMute, vm, Engine, Tracker.ChannelStatuses);
            else
                ChannelSettings.HandleMute(channelToMute, vm, Engine, Tracker.ChannelStatuses);

            // handle solo of channel
            int channelToSolo =
                ChannelSettings.HandleChannelButtonClick(
                    x,
                    Tracker.SoloButtonStartPositionsX,
                    Tracker.ChannelCount,
                    Tracker.ChannelButtonSize
                );
            ChannelSettings.HandleSolo(channelToSolo, vm, Engine, Tracker.ChannelStatuses, Tracker.ChannelCount);
        }

        /// <summary>
        /// Method to prevent key presses from changing the selected instrument in the SoundFont grid.
        /// </summary>
        private void ListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z) // prevent key presses moving focus in the preset select window
                e.Handled = true;
        }

        /// <summary>
        /// Method to handle adding a new pattern.
        /// </summary>
        private void AddPatternButton_Click(object sender, RoutedEventArgs e)
        {
            Tracker.Patterns.Insert(
                Tracker.CurrentPatternIndex + 1, 
                new Pattern(
                    rowCount: ProgramConstants.MaxRowCount,
                    channels: ProgramConstants.DefaultChannelCount
                )
            );
            FrameVerticalScrollBar.Value += FrameVerticalScrollBar.SmallChange;
            InitialiseTracker(SelectedSoundFont.Text, (List<Pattern>)Tracker.Patterns, vm.BPM);
        }

        /// <summary>
        /// Method to handle remove an existing pattern.
        /// </summary>
        private void RemovePatternButton_Click(object sender, RoutedEventArgs e)
        {
            if (Tracker.Patterns.Count == 1) { return; } // don't allow removal when only one pattern exists
            if (Tracker.CurrentPatternIndex == Tracker.Patterns.Count - 1)
                FrameVerticalScrollBar.Value -= FrameVerticalScrollBar.SmallChange;
            
            Tracker.Patterns.RemoveAt(Tracker.CurrentPatternIndex); // TODO: consider making it more clear that current pattern is being deleted
            InitialiseTracker(SelectedSoundFont.Text, (List<Pattern>)Tracker.Patterns, ProgramConstants.DefaultBPM);
        }

        /// <summary>
        /// Method used for number only text fields to prevent other characters from being accepted.
        /// </summary>
        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// Method to update tracker settings when clicking away from the settings panel.
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            Tracker.Focus();
            UpdateTrackerSettings();
        }

        /// <summary>
        /// Method to handle click of the up/down arrows for each of the tracker settings fields.
        /// </summary>
        private void SettingsArrow_Click(object sender, RoutedEventArgs e)
        {
            RepeatButton button = (RepeatButton)sender;
            string tag = button.Tag.ToString();

            // if SHIFT held, increase the change increment
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

        /// <summary>
        /// Method to update tracker settings based on view model data.
        /// </summary>
        private void UpdateTrackerSettings()
        {
            // for each below, only perform the update if the value has changed
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
                InitialiseTracker(SelectedSoundFont.Text, Engine.Tracker.Patterns, vm.BPM); // need to re-initialise for this change
            }
            else if (vm.RowHighlight != Tracker.RowHighlight)
            {
                Tracker.RowHighlight = vm.RowHighlight;
            }
        }

        /// <summary>
        /// Method to apply changes to settings fields when pressing the Enter key.
        /// </summary>
        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
                Tracker.Focus();
                UpdateTrackerSettings();
            }
        }

        /// <summary>
        /// Method to update buttons and settings field to enabled/disabled based on current context.
        /// </summary>
        private void HandleButtonState(bool isEnabled)
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

        /// <summary>
        /// Method to handle Create New Project button click.
        /// </summary>
        private void CreateNewButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = GetConfirmationDialog("Create New Project");
            if (!confirmed) { return; }

            // reset view model data to defaults
            vm.SetViewModelData(
                ProgramConstants.DefaultBPM,
                ProgramConstants.DefaultSpeed,
                ProgramConstants.DefaultRowCount,
                ProgramConstants.DefaultRowHighlight,
                ""
            );
            vm.ResetColumns();

            // reset to first row, clear project title and re-initialise tracker with defaults
            Tracker.GlobalCurrentRow = 0;
            ProjectTitle = "";
            InitialiseTracker(
                soundFont: ProgramConstants.DefaultSoundFont,
                patterns: [
                    new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount),
                ],
                bpm: ProgramConstants.DefaultBPM
            );
        }

        /// <summary>
        /// Method to handle Save Project and Save Project As... button clicks.
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = (System.Windows.Controls.Button)sender;
            string tag = button.Tag.ToString();
            SaveProject(ProjectTitle, isNewProject: tag == "Save_As"); // pass in tag to use different save logic
        }

        /// <summary>
        /// Method to handle Load Project button click.
        /// </summary>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = GetConfirmationDialog("Load Project");
            if (!confirmed) { return; }

            string fileName = GetOpenFileDialog(title: "Select .sft file", filter: "sfTracker Files (*.sft)|*.sft");
            if (fileName == "") { return; }

            LoadProject(GetParsedFileName(fileName));
        }

        /// <summary>
        /// Method to save existing and new projects.
        /// </summary>
        private void SaveProject(string filePath, bool isNewProject = false)
        {
            // for new projects, open a save dialog and update project title
            if (isNewProject || filePath == "")
            {
                string fileName = GetSaveFileDialog(title: "Save project", filter: "sfTracker Files (*.sft)|*.sft");
                if (fileName == "") { return; }
                filePath = GetParsedFileName(fileName);
                ProjectTitle = filePath;
                vm.WindowTitle = filePath;
            }

            // resize patterns to only save up to the number of rows displayed in the project.
            // this prevents files from storing rows with nothing in them, reducing file size significantly
            // TODO: include channel count (if i'm making them generic)
            List<Pattern> resizedPatterns = [];
            foreach (var pattern in Engine.Tracker.Patterns)
            {
                Pattern resizedPattern = new Pattern(rowCount: vm.RowCount, channels: ProgramConstants.DefaultChannelCount);
                for (int i = 0; i < vm.RowCount; i++)
                    resizedPattern.Rows[i] = pattern.Rows[i];

                resizedPatterns.Add(resizedPattern);
            }

            // create project skeleton for saving
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

            // save file using Json serialiser
            JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(project, jsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Method to load a project.
        /// </summary>
        private void LoadProject(string filePath)
        {
            // read data from Json
            string json = File.ReadAllText(filePath);
            ProjectFile project = JsonSerializer.Deserialize<ProjectFile>(json);

            // update view model data
            vm.SetViewModelData(project.BPM, project.Speed, project.RowCount, project.RowHighlight, project.ProjectName);

            // refill unused row data which was removed during save
            // TODO: make this generic and use between both save and load
            List<Pattern> resizedPatterns = [];
            foreach (var pattern in project.Patterns)
            {
                Pattern filledPattern = new Pattern(rowCount: ProgramConstants.MaxRowCount, channels: ProgramConstants.DefaultChannelCount);
                for (int i = 0; i < project.RowCount; i++)
                    filledPattern.Rows[i] = pattern.Rows[i];

                resizedPatterns.Add(filledPattern);
            }

            // update project information and settings, then re-initialise tracker
            ProjectTitle = filePath;
            vm.ResetColumns();
            UpdateTrackerSettings();
            InitialiseTracker(project.SoundFont, resizedPatterns, project.BPM);
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

        /// <summary>
        /// Method to remove the full path from a file and return just its name.
        /// </summary>
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