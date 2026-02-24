using sfTracker.Audio;
using sfTracker.Playback;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace sfTracker
{
    public partial class MainWindow : Window
    {
        private SynthEngine engine;
        private readonly Stopwatch playbackClock = new();
        private readonly int defaultBPM = 140;
        public double currentRowPosition;
        public bool IsPlaying = false;
        private int totalRowCount = 0;
        private int currentRow = 0;
        private int currentColumn = 0;
        private bool preventScrollbarEvent;

        public MainWindow()
        {
            InitializeComponent();
            InitialiseTracker(
                soundFont: "Kirby's_Dream_Land_3.sf2",
                patterns: [new Pattern(rowCount: 32, channels: 4), new Pattern(rowCount: 32, channels: 4)],
                BPM: defaultBPM
            );
            VerticalScrollBar.Maximum = totalRowCount;
            HorizontalScrollBar.Maximum = 3; // TODO: make this the number of channels
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

            EnableEventListeners();
            Console.WriteLine("Tracker initialized!");
        }

        private void DisableEventListeners()
        {
            CompositionTarget.Rendering -= (s, e) => Tracker.SetCurrentRow(engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted -= StartPlayback;
            Tracker.VerticalScrollbarValueChanged -= SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged -= SetHorizontalScrollbarValue;
            Tracker.AdvancedRow -= AdvanceRow;
        }

        private void EnableEventListeners()
        {
            CompositionTarget.Rendering += (s, e) => Tracker.SetCurrentRow(engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted += StartPlayback;
            Tracker.VerticalScrollbarValueChanged += SetVerticalScrollbarValue;
            Tracker.HorizontalScrollbarValueChanged += SetHorizontalScrollbarValue;
            Tracker.AdvancedRow += AdvanceRow;
        }

        public void StartRendering()
        {
            CompositionTarget.Rendering += OnFrame;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double time = playbackClock.Elapsed.TotalSeconds;

            double rowDuration = (60.0 / (engine.Tracker.BPM * engine.Tracker.TicksPerBeat)) * engine.Tracker.Speed;

            Tracker.CurrentRowPosition = Math.Floor(time / rowDuration) % totalRowCount;

            SetVerticalScrollbarValue(Tracker.CurrentRowPosition);

            InvalidateVisual();
        }

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    engine.Start();

        //    if (!IsPlaying)
        //    {
        //        IsPlaying = true;
        //        playbackClock.Restart();
        //        CompositionTarget.Rendering += OnFrame;
        //    }
        //    else
        //    {
        //        IsPlaying = false;
        //        playbackClock.Stop();
        //        CompositionTarget.Rendering -= OnFrame;
        //    }

        //    InvalidateVisual();
        //    Console.WriteLine("Tracker started!");
        //}

        public void StartPlayback()
        {
            engine.Start();

            if (!IsPlaying)
            {
                //currentRow = 0;
                //SetVerticalScrollbarValue(0);
                //Tracker._firstVisibleRow = 0;
                IsPlaying = true;
                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
                playbackClock.Stop();
                currentRow = 0;
                SetVerticalScrollbarValue(0);
                Tracker._firstVisibleRow = 0;
                CompositionTarget.Rendering -= OnFrame;
            }

            InvalidateVisual();
            Console.WriteLine("Tracker started!");
        }

        public void SetVerticalScrollbarValue(double value)
        {
            preventScrollbarEvent = true;
            VerticalScrollBar.Value = value;
            preventScrollbarEvent = false;
        }

        public void SetHorizontalScrollbarValue(double value)
        {
            preventScrollbarEvent = true;
            HorizontalScrollBar.Value = value;
            preventScrollbarEvent = false;
        }

        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsPlaying) return;

            if (e.Delta > 0)
            {
                Tracker.MoveUp();
                SetVerticalScrollbarValue(VerticalScrollBar.Value - VerticalScrollBar.SmallChange);

                Tracker._firstVisibleRow -= 1;
            }
            else if (e.Delta < 0)
            {
                Tracker.MoveDown();
                SetVerticalScrollbarValue(VerticalScrollBar.Value + VerticalScrollBar.SmallChange);

                if (VerticalScrollBar.Value > totalRowCount)
                {
                    SetVerticalScrollbarValue(0);
                    Tracker._firstVisibleRow = 0;
                    return;
                }

                if (!IsPlaying && Tracker.currentPatternIndex == 0 && Tracker.currentRow < 8)
                    return;
                        
                Tracker._firstVisibleRow += 1;
            }

            if (Tracker._firstVisibleRow < 0)
                Tracker._firstVisibleRow = 0;

            if (Tracker._firstVisibleRow > totalRowCount - Tracker.VisibleRowCount)
                Tracker._firstVisibleRow = totalRowCount - Tracker.VisibleRowCount;
        }

        private void VerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsPlaying && preventScrollbarEvent)
                return;

            int prevRow = currentRow;
            currentRow = (int)e.NewValue;

            Console.WriteLine(prevRow + " " + currentRow);

            AdvanceRow(prevRow, currentRow);
        }

        private void HorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsPlaying && preventScrollbarEvent)
                return;

            Console.WriteLine(e.NewValue);
            int prevColumn = currentColumn;
            currentColumn = (int)Math.Round(e.NewValue);

            AdvanceColumn(prevColumn, currentColumn);
        }

        private void AdvanceRow(int prevRow, int currentRow) //TODO: fix behaviour, currently works fine until scrolled with mouse, then reverses logic??
            {

            Console.WriteLine(prevRow + " " + currentRow);

            if (currentRow == totalRowCount)
            {
                return;
            }

            if (Math.Abs(prevRow - currentRow) > 1) // TODO: fix moving down scroll then pressing play, not resetting properly
            {
                preventScrollbarEvent = true;
                SetVerticalScrollbarValue(0);
                Tracker._firstVisibleRow = 0;
                return;
            }

            if (prevRow == totalRowCount - 1 && currentRow == 0)
            {
                Tracker.MoveDown();
                Tracker._firstVisibleRow = 0;
                return;
            }

            if (prevRow > currentRow)
            {
                Tracker.MoveUp();
                Tracker._firstVisibleRow -= 1;
            }
            else if (prevRow < currentRow)
            {
                Tracker.MoveDown();

                if (!IsPlaying && Tracker.currentPatternIndex == 0 && Tracker.currentRow < 8)
                    return;

                Tracker._firstVisibleRow += 1;
            }

            if (Tracker._firstVisibleRow < 0)
                Tracker._firstVisibleRow = 0;

            if (Tracker._firstVisibleRow > totalRowCount - Tracker.VisibleRowCount)
                Tracker._firstVisibleRow = totalRowCount - Tracker.VisibleRowCount;

            Tracker.Focus();
        }

        private void AdvanceColumn(int prevColumn, int currentColumn)
        {
            if (prevColumn < currentColumn)
            {
                Tracker.MoveRight();
            }
            else if (prevColumn > currentColumn)
            {
                Tracker.MoveLeft();
            }
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
                SelectedSoundFont.Text = $"{soundFontName}"; // TODO: figure out how to style
                Console.WriteLine($"{soundFontName}, {engine.Tracker.Patterns}, {defaultBPM}");
                InitialiseTracker(soundFontName, engine.Tracker.Patterns, defaultBPM);
            }
        }

        //// Detect when the mouse is over the grid and continuously allow scrolling
        //private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
        //{
        //    // Ensure the mouse is still inside the grid
        //    var position = e.GetPosition(this);

        //    var gridPosition = Tracker.TransformToAncestor(this).Transform(new Point(0, 0));

        //    var gridWidth = Tracker.ActualWidth;
        //    var gridHeight = Tracker.ActualHeight;

        //    // Calculate the boundaries of the grid

        //    double gridLeft = gridPosition.X;
        //    double gridTop = gridPosition.Y;
        //    double gridRight = gridLeft + gridWidth;
        //    double gridBottom = gridTop + gridHeight;

        //    if (position.X >= gridLeft && position.X <= gridRight && position.Y >= gridTop && position.Y <= gridBottom)
        //    {
        //        // If the mouse is within the grid, continue allowing scroll
        //        if (!Tracker.IsMouseCaptured)
        //        {
        //            Tracker.CaptureMouse();  // Ensure the mouse is captured while inside the grid
        //        }
        //    }
        //    else
        //    {
        //        // If the mouse is outside the grid, stop capturing the mouse
        //        Tracker.ReleaseMouseCapture();
        //    }
        //}
    }
}