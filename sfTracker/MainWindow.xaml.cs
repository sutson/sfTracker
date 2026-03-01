using sfTracker.Audio;
using sfTracker.GUI;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
        private int totalRowCount = 0;
        private int currentColumn = 0;
        private bool isPlaybackScrolling = false;
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
                patterns: [new Pattern(rowCount: 64, channels: 4), new Pattern(rowCount: 64, channels: 4)],
                BPM: defaultBPM
            );

            VerticalScrollBar.Maximum = totalRowCount;
            HorizontalScrollBar.Maximum = 3; // TODO: make this the number of channels

            //this.KeyDown += new System.Windows.Input.KeyEventHandler(OnKeyPress);
            //this.KeyUp += new System.Windows.Input.KeyEventHandler(OnKeyRelease);
        }

        private void LoadSoundFont(string path)
        {
            vm.LoadSoundFont(path);
        }

        private void InitialiseTracker(string soundFont, Pattern[] patterns, int BPM)
        {
            engine?.ResetTracker();
            engine?.Dispose();
            DisableEventListeners();

            engine = new SynthEngine(soundFont);
            engine.Tracker.Patterns = patterns;
            engine.Tracker.SetBPM(BPM);

            Tracker.Patterns = engine.Tracker.Patterns;
            Tracker.Engine = engine;
            Tracker.Focus();

            for (int i = 0; i < Tracker.Patterns.Count; i++)
                totalRowCount += Tracker.Patterns[i].RowCount;

            LoadSoundFont(soundFont);
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
                Tracker.SelectInstrument(vm.SelectedPreset.Instrument);
                Tracker.SelectBank(vm.SelectedPreset.Bank);
            }
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double time = playbackClock.Elapsed.TotalSeconds;

            double rowDuration = (60.0 / (engine.Tracker.BPM * engine.Tracker.TicksPerBeat)) * engine.Tracker.Speed;

            Tracker.CurrentRowPosition = Math.Floor(time / rowDuration) % totalRowCount;

            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);

            Tracker.GlobalCurrentRow = (int)Tracker.CurrentRowPosition;

            InvalidateVisual();
        }

        public void StartPlayback()
        {
            engine.Start();

            if (!IsPlaying)
            {
                IsPlaying = true;
                isPlaybackScrolling = true;
                
                Tracker.PatternCurrentRow = 0;
                Tracker.FirstVisibleRow = 0;
                VerticalScrollBar.Value = 0;
                isPlaybackScrolling = false;

                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
                playbackClock.Stop();
                isPlaybackScrolling = true;

                Tracker.PatternCurrentRow = 0;
                Tracker.SetCurrentRow(0);

                isPlaybackScrolling = false;

                CompositionTarget.Rendering -= OnFrame;
            }

            InvalidateVisual();
            //Console.WriteLine("Tracker started!");
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

        //private void AdvanceRow(int prevRow, int currentRow)
        //{
        //    if (currentRow == totalRowCount)
        //    {
        //        Tracker._firstVisibleRow = 0;
        //        VerticalScrollBar.Value = 0;
        //        Tracker.InvalidateVisual();
        //        return;
        //    }

        //    if (currentRow == prevRow)
        //        return;

        //    //Console.WriteLine($"Curr: {currentRow}, Prev: {prevRow}");
        //    int difference = currentRow - prevRow;


        //    if (difference > 0)
        //    {
        //        if (difference > 0)
        //        {
        //            for (int i = 0; i < difference; i++)
        //                Tracker.MoveDown();
        //        }
        //        else
        //        {
        //            for (int i = 0; i < Math.Abs(difference); i++)
        //                Tracker.MoveUp();
        //        }

        //        //EnsureCursorVisible();
        //        Tracker.InvalidateVisual();
        //    }
        //    else
        //    {
        //        for (int i = 0; i < Math.Abs(difference); i++)
        //        {
        //            Tracker.MoveUp();
        //            Tracker._firstVisibleRow--;
        //        }
        //    }

        //    if (Tracker._firstVisibleRow < 0)
        //        Tracker._firstVisibleRow = 0;

        //    if (Tracker._firstVisibleRow > totalRowCount - Tracker.VisibleRowCount)
        //        Tracker._firstVisibleRow = totalRowCount - Tracker.VisibleRowCount;

        //    Tracker.Focus();
        //}

        private void Tracker_RowChanged(int newRow)
        {
            _internalScrollChange = true;
            VerticalScrollBar.Value = newRow;
            _internalScrollChange = false;
        }

        private void Tracker_ColumnChanged(int newColumn)
        {
            _internalScrollChange = true;
            HorizontalScrollBar.Value = newColumn;
            _internalScrollChange = false;
        }

        //private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    //if (isPlaybackScrolling)
        //    //    return;

        //    //int prevRow = Tracker.GetCurrentGlobalRow();
        //    //int currentRow = (int)e.NewValue;

        //    //AdvanceRow(prevRow, currentRow);
        //    //int newValue = (int)e.NewValue;
        //    //Console.WriteLine((int)e.NewValue);
        //    //if (newValue == totalRowCount)
        //    //    Tracker._firstVisibleRow = 0;
        //    //else
        //    //    Tracker._firstVisibleRow = (int)e.NewValue;

        //    //Tracker.InvalidateVisual();
        //    //Tracker.Focus();

        //    //int value = (int)e.NewValue;

        //    //if (value == totalRowCount)
        //    //{
        //    //    WrapToTop();
        //    //    return;
        //    //}

        //    //Tracker._firstVisibleRow = value;
        //    //Tracker.InvalidateVisual();

        //    if (_internalScrollChange)
        //        return;

        //    int targetRow = (int)e.NewValue;
        //    int difference = targetRow - Tracker.currentRow;

        //    if (difference > 0)
        //    {
        //        for (int i = 0; i < difference; i++)
        //            Tracker.MoveDown();
        //            EnsureCursorVisible();
        //            InvalidateVisual();
        //    }
        //    else if (difference < 0)
        //    {
        //        for (int i = 0; i < Math.Abs(difference); i++)
        //            Tracker.MoveUp();
        //            EnsureCursorVisible();
        //            InvalidateVisual();
        //    }

        //    Tracker.Focus();
        //}

        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Tracker.GlobalCurrentRow = (int)e.NewValue;
            Tracker.Focus();
        }

        private bool _internalScrollChange = false;

        private void HorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //if (isPlaybackScrolling)
            //    return;

            Tracker.GlobalCurrentColumn = (int)e.NewValue;
            Tracker.Focus();

            //int prevColumn = Tracker.CurrentChannel;
            //int currentColumn = (int)Math.Round(e.NewValue);

            //AdvanceColumn(prevColumn, currentColumn);
        }

        //private void AdvanceColumn(int prevColumn, int currentColumn)
        //{
        //    Console.WriteLine($"Prev: {prevColumn}, Curr: {currentColumn}");

        //    if (currentColumn == 0 || currentColumn == 4)
        //    {
        //        return;
        //    }

        //    if (currentColumn == prevColumn)
        //        return;

        //    //Console.WriteLine($"Curr: {currentRow}, Prev: {prevRow}");
        //    int difference = currentColumn - prevColumn;

        //    if (difference > 0)
        //    {
        //        for (int i = 0; i < difference; i++)
        //        {
        //            Tracker.MoveRight();
        //        }
        //    }
        //    else
        //    {
        //        for (int i = 0; i < Math.Abs(difference); i++)
        //        {
        //            Tracker.MoveLeft();
        //        }
        //    }

        //    Tracker.Focus();
        //    //if (prevColumn < currentColumn)
        //    //{
        //    //    Tracker.MoveRight();
        //    //}
        //    //else if (prevColumn > currentColumn)
        //    //{
        //    //    Tracker.MoveLeft();
        //    //}
        //    //Tracker.Focus();
        //}

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
                SelectedSoundFont.Text = $"{soundFontName}";
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