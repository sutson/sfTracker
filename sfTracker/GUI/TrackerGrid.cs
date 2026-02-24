using sfTracker.Audio;
using sfTracker.Playback;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace sfTracker;
public class TrackerGrid : FrameworkElement
{
    public int currentRow = 0;
    public int currentChannel = 0;
    public int currentPatternIndex = 0; // index in Patterns[]
    public int _firstVisibleRow = 0;
    public int TotalRowCount = 0;
    public double VerticalScrollbarValue = 0;
    public double HorizontalScrollbarValue = 0;

    public double RowHeight = 15;
    private const double ColumnWidth = 120;
    private readonly int LeadInRows = 8; // number of rows before the viewport starts scrolling

    private static SolidColorBrush FourthRowHighlight;
    private static SolidColorBrush InactiveFourthRowHighlight;
    private static SolidColorBrush CurrentCellHighlight;
    private static SolidColorBrush CurrentRowHighlight;
    private static SolidColorBrush ActivePatternBrush;
    private static SolidColorBrush InactivePatternBrush;
    private static SolidColorBrush LowOpacityTextBrush;

    private static SolidColorBrush InactiveTextBrush;
    private static SolidColorBrush LowOpacityInactiveTextBrush;

    public SynthEngine Engine { get; set; }

    public TrackerGrid()
    {
        FourthRowHighlight = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
        FourthRowHighlight.Freeze();

        InactiveFourthRowHighlight = new SolidColorBrush(Color.FromArgb(5, 255, 255, 255));
        InactiveFourthRowHighlight.Freeze();

        CurrentCellHighlight = new SolidColorBrush(Color.FromArgb(80, 0, 120, 215));
        CurrentCellHighlight.Freeze();

        CurrentRowHighlight = new SolidColorBrush(Color.FromArgb(40, 10, 255, 255));
        CurrentRowHighlight.Freeze();

        ActivePatternBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        ActivePatternBrush.Freeze();

        InactivePatternBrush = new SolidColorBrush(Color.FromArgb(80, 200, 200, 200)); // greyed
        InactivePatternBrush.Freeze();

        LowOpacityTextBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)); // greyed
        LowOpacityTextBrush.Freeze();

        InactiveTextBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
        InactiveTextBrush.Freeze();

        LowOpacityInactiveTextBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)); // greyed
        LowOpacityInactiveTextBrush.Freeze();

        Focusable = true; // set focusable so key presses can be used to navigate/place notes
    }

    // https://stackoverflow.com/questions/47678298/wpf-dependency-property-on-change-update-control

    public static readonly DependencyProperty PatternProperty = DependencyProperty.Register(
        nameof(Patterns),
        typeof(IList<Pattern>),
        typeof(TrackerGrid),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public IList<Pattern> Patterns
    {
        get => (IList<Pattern>)GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    //

    public static readonly DependencyProperty CurrentRowPositionProperty =
    DependencyProperty.Register(
        nameof(CurrentRowPosition),
        typeof(double),
        typeof(TrackerGrid),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public double CurrentRowPosition
    {
        get => (double)GetValue(CurrentRowPositionProperty);
        set => SetValue(CurrentRowPositionProperty, value);
    }

    public bool IsPlaying = false;

    public void SetCurrentRow(int row)
    {
        CurrentRowPosition = row;
        InvalidateVisual();
    }

    // Total number of rows
    public int TotalRows { get; set; } = 64;

    // Current row index
    public int CurrentRow { get; private set; } = 0;

    public int VisibleRowCount => (int)(ActualHeight / RowHeight) + 1;

    private int GetCurrentGlobalRow()
    {
        int globalRow = currentRow;

        for (int i = 0; i < currentPatternIndex; i++)
            globalRow += Patterns[i].RowCount;

        return globalRow;
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        if (Patterns == null || Patterns.Count == 0) return;

        int globalRowOffset = 0;

        int currentGlobalRow = GetCurrentGlobalRow();

        for (int i = 0; i < Patterns.Count; i++)
        {
            // TODO: fix currentPatternIndex to be the actual pattern being played instead of the selected pattern
            var brush = (i == currentPatternIndex) ? ActivePatternBrush : InactivePatternBrush;

            int rowCount = Patterns[i].RowCount; // get number of rows in current pattern
            int channelCount = Patterns[i].Rows[0].Cells.Length; // get number of channels (columns) in current pattern

            //double visibleHeight = this.ActualHeight + 10 * RowHeight; // bottom half

            for (int row = 0; row < rowCount; row++)
            {
                int absoluteRow = globalRowOffset + row;

                double y = (absoluteRow - _firstVisibleRow) * RowHeight;

                // draw row number on the left
                if (y + (LeadInRows * RowHeight) >= 0)
                    context.DrawText(
                        RenderText(
                            GetRowString(absoluteRow),
                            (i == currentPatternIndex) ?
                                (row % 4 == 0 ? ActivePatternBrush : LowOpacityTextBrush) :
                                (row % 4 == 0 ? InactiveTextBrush : LowOpacityInactiveTextBrush)
                        ),
                        new Point(-31.5, y)
                    );

                for (int channel = 0; channel < channelCount; channel++)
                {
                    double x = channel * ColumnWidth; // horizontal width of tracker window
                    //double y = (globalRowOffset + row) * RowHeight;       // veritcal height of tracker window

                    var cell = Patterns[i].Rows[row].Cells[channel]; // cell at current position

                    if (y + (LeadInRows * RowHeight) < 0)
                        continue; // skip drawing rows outside the visible area

                    context.DrawText(RenderText(GetNoteText(cell.Note), brush), new Point(x + 5, y)); // draw --- or note value in cell

                    // highlight every fourth row like other trackers
                    if (row < rowCount && row % 4 == 0)
                    {
                        if (i == currentPatternIndex)
                            context.DrawRectangle(FourthRowHighlight, null, new Rect(0, y, channelCount * ColumnWidth, RowHeight));
                        else
                            context.DrawRectangle(InactiveFourthRowHighlight, null, new Rect(0, y, channelCount * ColumnWidth, RowHeight));
                    }

                    // highlight current cell (TODO: might make this highlight the whole row)
                    //if ((globalRowOffset + row) == currentRow && channel == currentChannel)
                    //    context.DrawRectangle(CurrentCellHighlight, null, new Rect(x, (globalCurrentRow * RowHeight), ColumnWidth, RowHeight));

                    if (absoluteRow == currentGlobalRow && channel == currentChannel)
                    {
                        double highlightY = (absoluteRow - _firstVisibleRow) * RowHeight; // TODO: fix this highlight lagging behind on restart?

                        context.DrawRectangle(
                            CurrentCellHighlight,
                            null,
                            new Rect(x, highlightY, ColumnWidth, RowHeight));
                    }
                }
            }

            globalRowOffset += rowCount;
        }

        // Highlight current row
        if (IsPlaying)
        {
            context.DrawRectangle(
                CurrentRowHighlight,
                null,
                new Rect(
                    0,
                    (CurrentRowPosition - _firstVisibleRow) * RowHeight, // TODO: play around with this to get scrolling right
                    4 * ColumnWidth, // TODO: change to generic channel count
                    RowHeight
                ));
        }
    }

    public FormattedText RenderText(string noteText, SolidColorBrush brush, FontWeight weight = default)
    {
        FormattedText text = 
            new FormattedText(
                noteText,                                   // text to be rendered
                CultureInfo.InvariantCulture,               // InvariantCulture ensures consistent text regardless of system culture 
                FlowDirection.LeftToRight,                  // write from left to right
                new Typeface("Consolas"),                   // Consolas is a nice monospace font
                14,                                         // font size
                brush,                                      // text colour
                VisualTreeHelper.GetDpi(this).PixelsPerDip  // prevent blurry text on high DPI monitors
            );

        text.SetFontWeight(weight);

        return text;
    }

    public string GetNoteText(int note)
    {
        if (note == -1) return "---";
        return Engine.Tracker.CalculateMidiNote(note);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Patterns == null || Patterns.Count == 0) return;

        int maxChannel = Patterns[0].Rows[0].Cells.Length - 1;

        //int maxRow = Pattern.RowCount - 1;

        switch (e.Key)
        {
            case Key.Up:
                AdvanceRow(CurrentRow, CurrentRow - 1);
                break;

            case Key.Down:
                AdvanceRow(CurrentRow, CurrentRow + 1);
                break;

            case Key.Left:
                MoveLeft();
                break;

            case Key.Right:
                MoveRight();
                break;

            case Key.Z:
                Patterns[currentPatternIndex].Rows[currentRow].Cells[currentChannel].Note = 60;
                SetNote(
                    pattern: currentPatternIndex, // TODO: update to use current pattern
                    row: currentRow,
                    channel: currentChannel,
                    note: 60, // TODO: update to map key press to MIDI value, not just C4
                    instrument: 13 // TODO: update to use selected instrument
                );
                break;

            case Key.X:
                Patterns[currentPatternIndex].Rows[currentRow].Cells[currentChannel].Note = 63;
                SetNote(
                    pattern: currentPatternIndex, // TODO: update to use current pattern
                    row: currentRow,
                    channel: currentChannel,
                    note: 63, // TODO: update to map key press to MIDI value, not just C4
                    instrument: 13 // TODO: update to use selected instrument
                );
                break;

            case Key.C:
                Patterns[currentPatternIndex].Rows[currentRow].Cells[currentChannel].Note = 67;
                SetNote(
                    pattern: currentPatternIndex, // TODO: update to use current pattern
                    row: currentRow,
                    channel: currentChannel,
                    note: 67, // TODO: update to map key press to MIDI value, not just C4
                    instrument: 13 // TODO: update to use selected instrument
                );
                break;

            case Key.Delete:
            case Key.Back:
                Patterns[currentPatternIndex].Rows[currentRow].Cells[currentChannel].Note = -1;
                SetNote(
                    pattern: currentPatternIndex, // TODO: update to use current pattern
                    row: currentRow,
                    channel: currentChannel,
                    note: -1, // TODO: update to map key press to MIDI value, not just C4
                    instrument: -1 // TODO: update to use selected instrument
                );
                break;

            case Key.Enter:
                StartPlayback();
                if (!IsPlaying)
                {
                    SetVerticalScrollbarValue(0);
                    currentRow = 0;
                    _firstVisibleRow = 0;
                }
                break;
        }

        Focus();
        InvalidateVisual();
        e.Handled = true;
    }

    public event Action PlaybackStarted;

    public void StartPlayback()
    {
        PlaybackStarted?.Invoke();
        IsPlaying = !IsPlaying;
    }

    public event Action<double> VerticalScrollbarValueChanged;

    public void SetVerticalScrollbarValue(double value)
    {
        VerticalScrollbarValueChanged?.Invoke(value);
        VerticalScrollbarValue = value;
    }

    public event Action<double> HorizontalScrollbarValueChanged;

    public void SetHorizontalScrollbarValue(double value)
    {
        HorizontalScrollbarValueChanged?.Invoke(value);
        HorizontalScrollbarValue = value;
    }

    public event Action<int, int> AdvancedRow;

    public void AdvanceRow(int cur, int next)
    {
        AdvancedRow?.Invoke(cur, next);
    }

    private string GetRowString(int absoluteRow)
    {
        int rowText = absoluteRow;
        if (currentPatternIndex > 0)
        {
            if (rowText >= Patterns[currentPatternIndex - 1].RowCount)
                rowText -= Patterns[currentPatternIndex - 1].RowCount;
        }
        else
        {
            if (rowText >= Patterns[0].RowCount)
                rowText -= Patterns[0].RowCount;
        }

        var rowStr = rowText.ToString();

        return rowStr.PadLeft(3, '0');
    }

    private void SetNote(int pattern, int row, int channel, int note, int instrument)
    {
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Channel = channel;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Note = note;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Instrument = instrument;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        // TODO: set clicked cell as current row/channel

        //var position = e.GetPosition(this);
        
        //var x = position.X;
        //var y = position.Y;

        //var fullX = Patterns[0].Rows[0].Cells.Length;
        //var fullY = Patterns[0].Rows.Length;

        //var cellX = Math.Floor(fullX / x);
        //var cellY = Math.Floor(fullY / y);


        //Console.WriteLine(cellX + " " + cellY);

        //Console.WriteLine(e.GetPosition(this));
        //currentRow = 3;
        Focus(); // sets keyboard focus to this control
    }

    public void MoveUp()
    {
        if (currentRow > 0)
        {
            currentRow--;
            VerticalScrollbarValue -= 1;
            SetVerticalScrollbarValue(VerticalScrollbarValue);
        }
        else if (currentPatternIndex > 0)
        {
            // move to last row of previous pattern
            currentPatternIndex--;
            currentRow = Patterns[currentPatternIndex].RowCount - 1;
        }
    }

    public void MoveDown()
    {
        if (currentRow < Patterns[currentPatternIndex].RowCount - 1)
        {
            currentRow++;
        }
        else
        {
            currentRow = 0;
            if (currentPatternIndex < Patterns.Count - 1)
            {
                currentPatternIndex++;
            }
            else
            {
                currentPatternIndex = 0;
                _firstVisibleRow = 0;
                SetVerticalScrollbarValue(0);
                return;
            }
        }

        SetVerticalScrollbarValue(VerticalScrollbarValue + 1);

        //currentRow++;
        //VerticalScrollbarValue += 1;
        //SetVerticalScrollbarValue(VerticalScrollbarValue);

        //if (currentRow == Patterns[currentPatternIndex].RowCount)
        //{
        //    currentPatternIndex++;

        //    if (currentPatternIndex == Patterns.Count)
        //    {
        //        currentRow = 0;
        //        currentPatternIndex = 0;
        //        _firstVisibleRow = 0;
        //    }
        //}



        //Console.WriteLine($"Current Row: {currentRow}, Current Pattern: {currentPatternIndex}, Row Count: {Patterns[currentPatternIndex].RowCount}");
        //if (currentRow < Patterns[currentPatternIndex].RowCount - 1)
        //{
        //    currentRow++;
        //}
        //else
        //{
        //    currentRow = 0;
        //    if (currentPatternIndex < Patterns.Count - 1)
        //    {
        //        currentPatternIndex++;

        //    }
        //    else
        //    {
        //        currentPatternIndex = 0;
        //    }
        //}


    }

    public void MoveLeft()
    {
        if (currentChannel > 0)
        {
            currentChannel--;
            HorizontalScrollbarValue -= 1;
            SetHorizontalScrollbarValue(HorizontalScrollbarValue);
        }
    }

    public void MoveRight()
    {
        Console.WriteLine(currentChannel + " " + Patterns[0].Rows[0].Cells.Length);
        if (currentChannel < Patterns[0].Rows[0].Cells.Length - 1)
        {
            currentChannel++;
            HorizontalScrollbarValue += 1;
            SetHorizontalScrollbarValue(HorizontalScrollbarValue);
        }
    }
}
