using MeltySynth;
using sfTracker.Audio;
using sfTracker.GUI;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace sfTracker
{
    public partial class MainWindow : Window
    {
        private SynthEngine engine;
        private readonly Stopwatch playbackClock = new();
        private readonly MainViewModel vm;
        private readonly int defaultBPM = 120;
        private bool isPlaybackScrolling = false;
        private int totalRowCount = 0;
        private bool internalScrollChange = false;
        private double lastHorizontalScrollValue;
        private int defaultChannelCount = 4;
        private int defaultRowCount = 32;
        private double resumePlaybackRow = 0;
        private List<PatternBoundary> patternBoundaries = [];
        private bool IsClickingCell = false;

        public double currentRowPosition;
        public bool IsPlaying = false;
        public int currentlyPlayingNote;

        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
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
            HorizontalScrollBar.Maximum = Tracker.ChannelCount * Tracker.ColumnsPerChannel - 1;

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
            engine?.ResetTracker(0);
            engine?.Dispose();
            DisableEventListeners();

            engine = new SynthEngine(soundFont);
            engine.Tracker.Patterns = patterns;
            engine.Tracker.ActiveVoices = new Voice[defaultChannelCount];
            engine.Tracker.CurrentVolumes = new int[defaultChannelCount];
            engine.Tracker.TargetVolumes = new int[defaultChannelCount];
            engine.Tracker.SetBPM(BPM);
            
            Tracker.Patterns = engine.Tracker.Patterns;
            Tracker.Engine = engine;
            Tracker.Focus();

            //for (int i = 0; i < Tracker.Patterns.Count; i++)
            //    totalRowCount += Tracker.Patterns[i].RowCount;

            ComputePatternBoundaries();

            LoadSoundFont(soundFont);
            SelectedSoundFont.Text = $"{soundFont}";
            EnableEventListeners();
            Console.WriteLine("Tracker initialized!");
        }

        private void DisableEventListeners()
        {
            CompositionTarget.Rendering -= (s, e) => Tracker.SetCurrentRow(engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted -= StartPlayback;
            Tracker.VerticalScrollbarValueChanged -= SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged -= SetHorizontalScrollbarValue;
            Tracker.RowChanged -= Tracker_RowChanged;
            Tracker.ColumnChanged -= Tracker_ColumnChanged;
            vm.PropertyChanged -= InstrumentChanged;
        }

        private void EnableEventListeners()
        {
            CompositionTarget.Rendering += (s, e) => Tracker.SetCurrentRow(engine.Tracker.CurrentRow);
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

            double rowDuration = (60.0 / (engine.Tracker.BPM * engine.Tracker.TicksPerBeat)) * engine.Tracker.Speed;

            double rowsAdvanced = Math.Floor(time / rowDuration);
            
            Tracker.CurrentRowPosition = (rowsAdvanced + resumePlaybackRow) % totalRowCount;

            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);

            Tracker.GlobalCurrentRow = (int)Tracker.CurrentRowPosition;

            InvalidateVisual();
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
        }

        public void StartPlayback(int currentPattern)
        {
            engine.Start(currentPattern);

            int currentRow = (int)Tracker.GlobalCurrentRow;

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
                
                //Tracker.PatternCurrentRow = 0;
                //Tracker.FirstVisibleRow = 0;
                //VerticalScrollBar.Value = 0;

                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
                playbackClock.Stop();
                

                //Tracker.PatternCurrentRow = 0;
                //Tracker.SetCurrentRow(0); TODO: check if this is needed?

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
            internalScrollChange = true;
            VerticalScrollBar.Value = newRow;
            internalScrollChange = false;
        }

        private void Tracker_ColumnChanged(int newColumn)
        {
            internalScrollChange = true;
            HorizontalScrollBar.Value = newColumn;
            internalScrollChange = false;
        }

        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Tracker.GlobalCurrentRow = (int)e.NewValue;
            Tracker.Focus();
        }

        private void HorizontalScrollBar_VaueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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
            if (IsPlaying) { return;  }
            double mousePosX = e.GetPosition(this).X;
            double mousePosY = e.GetPosition(this).Y;

            double startingPosX = 50; // TODO: make generic
            double startingPosY = 470 - (Tracker.RowHeight * Tracker.FirstVisibleRow); // TODO: make generic
            double absoluteX = mousePosX - startingPosX;
            double absoluteY = mousePosY - startingPosY;

            if (
                absoluteX < 0 || absoluteX > Tracker.ColumnWidth * Tracker.ChannelCount ||
                absoluteY < 0 || absoluteY > Tracker.RowHeight * totalRowCount
            ) { return; }
            GetCellFromCoord(absoluteX, absoluteY);
        }

        private void GetCellFromCoord(double x, double y)
        {
            double channelWidth = Tracker.ColumnWidth;
            for (int channel = 0; channel < 4; channel++) // TODO: make generic
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
                        int cellColumnToSelect = channel * Tracker.ColumnsPerChannel + i;
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
                //Console.WriteLine($"{soundFontName}, {engine.Tracker.Patterns}, {defaultBPM}");
                InitialiseTracker(soundFontName, engine.Tracker.Patterns, defaultBPM);
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
        //        engine.Start();
        //        engine.Tracker.TriggerNoteWithKeyboard(
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
        //    engine.StopAudio();
        //    engine.Tracker.TriggerNoteWithKeyboard(
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