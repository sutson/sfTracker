using sfTracker.Audio;
using sfTracker.GUI;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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
        private readonly int defaultBPM = 120;
        private readonly int defaultChannelCount = 8;
        private readonly int defaultRowCount = 32;
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

        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
            vm.GetColumns(defaultChannelCount, Tracker.ColumnWidth);
            DataContext = vm;

            InitialiseTracker(
                soundFont: "Kirby's_Dream_Land_3.sf2",
                patterns: [
                    new Pattern(rowCount: defaultRowCount, channels: defaultChannelCount),
                    new Pattern(rowCount: defaultRowCount, channels: defaultChannelCount)
                ],
                BPM: defaultBPM
            );

            VerticalScrollBar.Maximum = totalRowCount - 1;
            HorizontalScrollBar.Maximum = Tracker.ChannelCount * Tracker.FieldsPerChannel - 1;

            //this.KeyDown += new System.Windows.Input.KeyEventHandler(OnKeyPress);
            //this.KeyUp += new System.Windows.Input.KeyEventHandler(OnKeyRelease);
        }

        private void LoadSoundFont(string path)
        {
            ObservableCollection<SoundFontPreset> presets = vm.LoadSoundFont(path);
            Tracker.PresetList = presets;
        }

        private void InitialiseTracker(string soundFont, Pattern[] patterns, int BPM)
        {
            Engine?.ResetTracker(0);
            Engine?.Dispose();
            DisableEventListeners();

            Engine = new SynthEngine(soundFont);
            Engine.Tracker.Patterns = patterns;
            Engine.Tracker.ActiveVoices = new Voice[defaultChannelCount];
            Engine.Tracker.CurrentVolumes = new int[defaultChannelCount];
            Engine.Tracker.TargetVolumes = new int[defaultChannelCount];
            Engine.Tracker.CurrentPannings = new PanEffect[defaultChannelCount];
            Engine.Tracker.TargetPannings = new PanEffect[defaultChannelCount];
            Engine.Tracker.ChannelMuteStatuses = new bool[defaultChannelCount];
            Engine.Tracker.SetBPM(BPM);
            
            Tracker.ChannelCount = defaultChannelCount;
            Tracker.MuteButtonStartPositionsX = new double[defaultChannelCount];
            Tracker.SoloButtonStartPositionsX = new double[defaultChannelCount];
            Tracker.ChannelStatuses = vm.Columns;
            Tracker.RowWidth = defaultChannelCount * Tracker.ColumnWidth;
            Tracker.Patterns = Engine.Tracker.Patterns;
            Tracker.Engine = Engine;
            Tracker.Focus();

            ComputePatternBoundaries();
            LoadSoundFont(soundFont);
            EnableEventListeners();
            SelectedSoundFont.Text = $"{soundFont}";
        }

        private void DisableEventListeners()
        {
            CompositionTarget.Rendering -= (s, e) => Tracker.SetCurrentRow(Engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted -= StartPlayback;
            Tracker.VerticalScrollbarValueChanged -= SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged -= SetHorizontalScrollbarValue;
            Tracker.RowChanged -= Tracker_RowChanged;
            Tracker.ColumnChanged -= Tracker_ColumnChanged;
            vm.PropertyChanged -= InstrumentChanged;
        }

        private void EnableEventListeners()
        {
            CompositionTarget.Rendering += (s, e) => Tracker.SetCurrentRow(Engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted += StartPlayback;
            Tracker.VerticalScrollbarValueChanged += SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged += SetHorizontalScrollbarValue;
            Tracker.RowChanged += Tracker_RowChanged;
            Tracker.ColumnChanged += Tracker_ColumnChanged;
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
            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);
            Tracker.GlobalCurrentRow = (int)Tracker.CurrentRowPosition;
        }

        private void ComputePatternBoundaries()
        {
            int currentRow = 0;

            for (int i = 0; i < Tracker.Patterns.Count; i++)
            {
                var pattern = Tracker.Patterns[i];

                patternBoundaries.Add(new PatternBoundary
                {
                    Index = i,
                    StartingRow = currentRow,
                    RowCount = pattern.RowCount
                });

                currentRow += pattern.RowCount;
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
                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
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

        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsPlaying) return;

            double change = e.Delta;

            if (change < 0)
            {
                SetVerticalScrollbarValue(VerticalScrollBar.Value + VerticalScrollBar.SmallChange);
                Tracker.GlobalCurrentRow++;
            }
            else if (change > 0)
            {
                SetVerticalScrollbarValue(VerticalScrollBar.Value - VerticalScrollBar.SmallChange);
                Tracker.GlobalCurrentRow--;
            }
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
            var dialog = new OpenFileDialog
            {
                Title = "Select .sf2 file",
                Filter = "SoundFont Files (*.sf2)|*.sf2",
                Multiselect = false
            };

            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string fileName = dialog.FileName;
                string soundFontName = fileName[(fileName.LastIndexOf('\\') + 1)..];
                InitialiseTracker(soundFontName, Engine.Tracker.Patterns, defaultBPM);
            }
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
            int channelToMute = HandleChannelButtonClick(x, Tracker.MuteButtonStartPositionsX);
            if (channelToMute != -1 && vm.Columns[channelToMute].IsMuted)
                HandleUnmute(channelToMute);
            else
                HandleMute(channelToMute);

            // for the solo button, handle solo accordingly
            int channelToSolo = HandleChannelButtonClick(x, Tracker.SoloButtonStartPositionsX);
            HandleSolo(channelToSolo);
        }

        private int HandleChannelButtonClick(double x, double[] startPositions)
        {
            for (int channel = 0; channel < Tracker.ChannelCount; channel++)
            {
                // check if current channel's button is within the x bounds of click
                // if it is, return the channel number
                double xLowerBound = startPositions[channel];
                double xUpperBound = startPositions[channel] + Tracker.ChannelButtonSize;
                if (x < xLowerBound || x > xUpperBound) { continue; }
                return channel;
            }

            // return -1 if no match found
            return -1;
        }

        private void HandleMute(int channel)
        {
            // do nothing if invalid channel or if channel already muted
            if (channel == -1 || vm.Columns[channel].IsMuted) { return; }

            UpdateMuteStatues(channel, isMuted: true); // update mute statuses for channel
            Engine.StopNoteInChannel(channel); // kill currently playing notes in channel
        }

        private void HandleUnmute(int channel)
        {
            // do nothing if invalid channel or if channel not already muted
            if (channel == -1 || !vm.Columns[channel].IsMuted) { return; }

            UpdateMuteStatues(channel, isMuted: false); // update mute statuses for channel

            // if unmuting a channel which isn't already solo
            // remove solo from all channels
            if (!vm.Columns[channel].IsSolo)
                foreach (var column in vm.Columns) { column.IsSolo = false; }
        }

        private void UpdateMuteStatues(int channel, bool isMuted)
        {
            vm.Columns[channel].IsMuted = isMuted; // update mute status
            Engine.Tracker.ChannelMuteStatuses[channel] = isMuted; // update channel statuses (used for preventing playback if muted)
            Tracker.ChannelStatuses = vm.Columns; // update channel statuses (used for GUI M/S button styling)
        }

        private void HandleSolo(int channel)
        {
            // if channel not valid, do nothing
            if (channel == -1) { return; }

            // get list of channels which should be muted
            List<int> channelsToMute = [.. Enumerable.Range(0, Tracker.ChannelCount).Where(x => x != channel)];
            
            if (vm.Columns[channel].IsSolo) // if channel is already solo
            {
                vm.Columns[channel].IsSolo = false; // remove solo setting
                for (int ch = 0; ch < Tracker.ChannelCount; ch++) // for each channel, remove mute
                    HandleUnmute(ch);
            }
            else // if channel not solo
            {
                vm.Columns[channel].IsSolo = true; // solo it
                if (vm.Columns[channel].IsMuted) // if the channel is currently muted, unmute it
                    HandleUnmute(channel);
                foreach (int ch in channelsToMute) // for all other channels, mute them if they aren't already and remove solo value
                {
                    vm.Columns[ch].IsSolo = false;
                    HandleMute(ch);
                }
            }
        }

        private void ListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z) // prevent key presses moving focus in the preset select window
                e.Handled = true;
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