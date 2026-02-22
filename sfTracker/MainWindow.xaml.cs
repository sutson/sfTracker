using sfTracker.Audio;
using sfTracker.Playback;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public double currentRowPosition;
        public bool IsPlaying = false;
        private int totalRowCount = 0;
        private int currentRow = 0;
        private bool preventScrollbarEvent;
        private int defaultBPM = 140;

        public MainWindow()
        {
            InitializeComponent();
            InitialiseTracker(
                soundFont: "Kirby's_Dream_Land_3.sf2",
                patterns: [new Pattern(rowCount: 32, channels: 4)],
                BPM: defaultBPM
            );
        }

        private void InitialiseTracker(string soundFont, Pattern[] patterns, int BPM)
        {
            engine?.ResetTracker();
            engine?.Dispose();

            engine = new SynthEngine(soundFont);
            engine.Tracker.Patterns = patterns;
            engine.Tracker.SetBPM(BPM);

            Tracker.Patterns = engine.Tracker.Patterns;
            Tracker.Engine = engine;
            Tracker.Focus();

            for (int i = 0; i < Tracker.Patterns.Count; i++)
                totalRowCount += Tracker.Patterns[i].RowCount;

            CompositionTarget.Rendering += (s, e) => Tracker.SetCurrentRow(engine.Tracker.CurrentRow);
            Tracker.PlaybackStarted += StartPlayback;
            Tracker.ScrollbarValueChanged += SetScrollbarValue;

            Console.WriteLine("Tracker initialized!");
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

            SetScrollbarValue(Tracker.CurrentRowPosition);

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
                IsPlaying = true;
                playbackClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                IsPlaying = false;
                playbackClock.Stop();
                CompositionTarget.Rendering -= OnFrame;
                SetScrollbarValue(0);
            }

            InvalidateVisual();
            Console.WriteLine("Tracker started!");
        }

        public void SetScrollbarValue(double value)
        {
            preventScrollbarEvent = true;
            TrackerScrollBar.Value = value;
            preventScrollbarEvent = false;
        }

        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsPlaying) return;

            if (e.Delta > 0)
            {
                Tracker.MoveUp();
                SetScrollbarValue(TrackerScrollBar.Value-TrackerScrollBar.SmallChange);

                Tracker._firstVisibleRow -= 1;
            }
            else if (e.Delta < 0)
            {

                Tracker.MoveDown();
                SetScrollbarValue(TrackerScrollBar.Value+TrackerScrollBar.SmallChange);

                if (!IsPlaying && Tracker.currentPatternIndex == 0 && Tracker.currentRow < 8)
                    return;
                        
                Tracker._firstVisibleRow += 1;
            }

            if (Tracker._firstVisibleRow < 0)
                Tracker._firstVisibleRow = 0;

            if (Tracker._firstVisibleRow > totalRowCount - Tracker.VisibleRowCount)
                Tracker._firstVisibleRow = totalRowCount - Tracker.VisibleRowCount;
            }

        private void TrackerScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsPlaying && preventScrollbarEvent)
                return;

            int prevRow = currentRow;
            currentRow = (int)e.NewValue;

            AdvanceRow(prevRow, currentRow);
        }

        private void AdvanceRow(int prevRow, int currentRow)
        {
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