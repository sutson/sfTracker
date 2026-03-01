using sfTracker.Audio;
using sfTracker.Controls;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace sfTracker.GUI;
public class TrackerGrid : FrameworkElement
{
    public int CurrentRow = 0;
    public int CurrentColumn = 0;
    public int PatternCurrentRow = 0;
    public int currentPatternIndex = 0; // index in Patterns[]
    public int FirstVisibleRow = 0;
    public int TotalRowCount = 0;
    public double VerticalScrollbarValue = 0;
    public double HorizontalScrollbarValue = 0;
    private int RowOffset = 0;
    private int SelectedBank = -1;
    private int SelectedInstrument = -1;
    public bool IsPlaying = false;

    public double RowHeight = 15;
    private const double ColumnWidth = 120;
    private readonly int LeadInRows = 8; // number of rows before the viewport starts scrolling
    public SynthEngine Engine { get; set; }

    public TrackerGrid()
    {
        Brushes.Init();
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

    public static readonly DependencyProperty CurrentRowPositionProperty = DependencyProperty.Register(
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

    public void SetCurrentRow(int row)
    {
        CurrentRowPosition = row;
        InvalidateVisual();
    }

    // Total number of rows
    public int TotalRows { get; set; } = 128; // TODO: update to calculate on init
    public int VisibleRowCount => (int)(ActualHeight / RowHeight) + 1;

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        //Point colSepStart = new Point(0, -209); TODO: decide if i want to render column seps like this
        //Point colSepEnd = new Point(0, 1000);

        //Pen linePen = new Pen(Brushes.ActivePatternBrush, 1);
        //context.DrawLine(linePen, colSepStart, colSepEnd);

        if (Patterns == null || Patterns.Count == 0) return;

        int globalRowOffset = 0;

        for (int i = 0; i < Patterns.Count; i++)
        {
            var brush = (i == currentPatternIndex) ? Brushes.ActivePatternBrush : Brushes.InactivePatternBrush;

            int rowCount = Patterns[i].RowCount; // get number of rows in current pattern
            int channelCount = Patterns[i].Rows[0].Cells.Length; // get number of channels (columns) in current pattern

            for (int row = 0; row < rowCount; row++)
            {
                int absoluteRow = globalRowOffset + row;

                double y = (absoluteRow - FirstVisibleRow) * RowHeight;

                // draw row number on the left
                if (y + (LeadInRows * RowHeight) >= 0)
                    context.DrawText(
                        RenderText(
                            GetRowString(absoluteRow),
                            (i == currentPatternIndex) ?
                                (row % 4 == 0 ? Brushes.ActivePatternBrush : Brushes.LowOpacityTextBrush) :
                                (row % 4 == 0 ? Brushes.InactiveTextBrush : Brushes.LowOpacityInactiveTextBrush)
                        ),
                        new Point(-31.5, y)
                    );

                for (int channel = 0; channel < channelCount; channel++)
                {
                    double x = channel * ColumnWidth; // horizontal width of tracker window

                    //colSepStart.X += ColumnWidth; TODO: decide if i want to render column seps like this
                    //colSepEnd.X += ColumnWidth;
                    //context.DrawLine(linePen, colSepStart, colSepEnd);

                    var cell = Patterns[i].Rows[row].Cells[channel]; // cell at current position

                    if (y + (LeadInRows * RowHeight) < 0)
                        continue; // skip drawing rows outside the visible area

                    context.DrawText(RenderText(GetNoteText(cell.Note), brush), new Point(x + 5, y)); // draw --- or note value in cell

                    // highlight every fourth row like other trackers
                    if (row < rowCount && row % 4 == 0)
                    {
                        if (i == currentPatternIndex)
                            context.DrawRectangle(Brushes.FourthRowHighlight, null, new Rect(0, y, channelCount * ColumnWidth, RowHeight));
                        else
                            context.DrawRectangle(Brushes.InactiveFourthRowHighlight, null, new Rect(0, y, channelCount * ColumnWidth, RowHeight));
                    }

                    if (absoluteRow == GlobalCurrentRow && channel == CurrentColumn)
                    {
                        double highlightY = (absoluteRow - FirstVisibleRow) * RowHeight; // TODO: fix this highlight lagging behind on restart?

                        context.DrawRectangle(
                            Brushes.CurrentCellHighlight,
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
                Brushes.CurrentRowHighlight,
                null,
                new Rect(
                    0,
                    (CurrentRowPosition - FirstVisibleRow) * RowHeight,
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

    public static string GetNoteText(int note)
    {
        if (note == -1) return "---";
        return TrackerEngine.CalculateMidiNote(note);
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

    private void PlaceNote(MidiNoteValueMap? note)
    {
        Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentColumn].Note = (int)note;
        SetNote(
            pattern: currentPatternIndex, // TODO: update to use current pattern
            row: PatternCurrentRow,
            channel: CurrentColumn,
            note: (int)note, // TODO: update to map key press to MIDI value, not just C4
            bank: SelectedBank,
            instrument: SelectedInstrument // TODO: update to use selected instrument
        );
        GlobalCurrentRow++;
    }

    private void DeleteNote(bool IsBackspace = false)
    {
        Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentColumn].Note = -1;
        SetNote(
            pattern: currentPatternIndex,
            row: PatternCurrentRow,
            channel: CurrentColumn,
            note: -1,
            bank: -1,
            instrument: -1
        );

        if (IsBackspace)
            GlobalCurrentRow--;
        else
            GlobalCurrentRow++;
    }

    private void SetNote(int pattern, int row, int channel, int note, int bank, int instrument)
    {
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Channel = channel;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Note = note;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Bank = bank;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Instrument = instrument;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Patterns == null || Patterns.Count == 0) return;

        MidiNoteValueMap? note = GetMidiNote(e.Key);
        if (note != null)
            PlaceNote(note);
        
        switch (e.Key)
        {
            case Key.Up:
                GlobalCurrentRow--;
                break;

            case Key.Down:
                GlobalCurrentRow++;
                break;

            case Key.Left:
                GlobalCurrentColumn--;
                break;

            case Key.Right:
                GlobalCurrentColumn++;
                break;

            case Key.Delete:
                DeleteNote();
                break;

            case Key.Back:
                DeleteNote(IsBackspace: true);
                break;

            case Key.Enter:
                StartPlayback();
                if (!IsPlaying)
                {
                    SetVerticalScrollbarValue(0);
                    SetCurrentRow(0);
                    FirstVisibleRow = 0;
                }
                break;
        }

        Focus();
        InvalidateVisual();
        e.Handled = true;
    }

    // === EVENT HANDLING === //

    public event Action PlaybackStarted;
    public event Action InstrumentSelected;
    public event Action BankSelected;
    public event Action<double> VerticalScrollbarValueChanged;
    public event Action<double> HorizontalScrollbarValueChanged;
    public event Action<int, int> AdvancedRow;
    public event Action<int, int> AdvancedColumn;
    public event Action<int> RowChanged;
    public event Action<int> ColumnChanged;

    public void StartPlayback()
    {
        PlaybackStarted?.Invoke();
        IsPlaying = !IsPlaying;
    }

    public void SelectInstrument(int value)
    {
        InstrumentSelected?.Invoke();
        SelectedInstrument = value;
    }

    public void SelectBank(int value)
    {
        BankSelected?.Invoke();
        SelectedBank = value;
    }
    public void SetVerticalScrollbarValue(double value)
    {
        VerticalScrollbarValueChanged?.Invoke(value);
        VerticalScrollbarValue = value;
    }
    public void SetHorizontalScrollbarValue(double value)
    {
        HorizontalScrollbarValueChanged?.Invoke(value);
        HorizontalScrollbarValue = value;
    }
    public void AdvanceRow(int cur, int next)
    {
        AdvancedRow?.Invoke(cur, next);
    }
    public void AdvanceColumn(int cur, int next)
    {
        AdvancedColumn?.Invoke(cur, next);
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
    public int GlobalCurrentRow
    {
        get => CurrentRow;
        set
        {
            if (CurrentRow == value)
                return;

            CurrentRow = WrapRow(value);

            EnsureVisible();
            InvalidateVisual();
            RowChanged?.Invoke(CurrentRow);
        }
    }

    public int GlobalCurrentColumn
    {
        get => CurrentColumn;
        set
        {
            if (CurrentColumn == value)
                return;

            CurrentColumn = WrapColumn(value);

            InvalidateVisual();
            ColumnChanged?.Invoke(CurrentColumn);
        }
    }

    public void EnsureVisible()
    {
        if (CurrentRow < FirstVisibleRow)
        {
            FirstVisibleRow = CurrentRow;
        }
        else if (CurrentRow >= FirstVisibleRow + VisibleRowCount)
        {
            FirstVisibleRow = CurrentRow - VisibleRowCount + 1;
        }
    }

    private int GetRowsInPreviousPattern(int index)
    {
        if (index == 0) return 0;
        return Patterns[index-1].Rows.Length;
    }

    private int WrapRow(int row)
    {
        int rowsInCurrentPattern = Patterns[currentPatternIndex].Rows.Length;

        if (row >= TotalRows)
        {
            RowOffset = 0;
            currentPatternIndex = 0;
            return 0;
        }

        if (row < 0)
            return 0;

        if (row - RowOffset == rowsInCurrentPattern)
        {
            RowOffset += rowsInCurrentPattern;
            currentPatternIndex++;
        }

        if (row < RowOffset)
        {
            RowOffset -= GetRowsInPreviousPattern(currentPatternIndex);
            currentPatternIndex--;
        }

        PatternCurrentRow = row - RowOffset;

        return row;
    }

    private static int WrapColumn(int column)
    {
       if (column > 3) // TODO: change to generic number of channels
            return 3;

        if (column < 0)
            return 0;
            
       return column;
    }

    public MidiNoteValueMap? GetMidiNote(Key key)
    {
        if (KeyboardToMidiNote.Map.TryGetValue(key, out var note))
            return note;

        return null;
    }
}
