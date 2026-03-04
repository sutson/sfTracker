using sfTracker.Audio;
using sfTracker.Common;
using sfTracker.Controls;
using sfTracker.Playback;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace sfTracker.GUI;
public class TrackerGrid : FrameworkElement
{
    private int RowCount = 0;
    private int RowOffset = 0;
    private int SelectedBank = -1;
    private int SelectedInstrument = -1;
    private int SelectedInstrumentID = -1;
    private int MaxRenderHeight = 500;

    public int CurrentRow = 0;
    public int CurrentColumn = 0;
    public int CurrentChannel = 0;
    public int ChannelCount = 4; // TODO: make generic
    public int PatternCurrentRow = 0;
    public int currentPatternIndex = 0; // index in Patterns[]
    public int FirstVisibleRow = 0;
    public int TotalRowCount = 0;
    public double VerticalScrollbarValue = 0;
    public double HorizontalScrollbarValue = 0;
    public bool IsPlaying = false;

    public ObservableCollection<SoundFontPreset> PresetList = [];
    public TrackerField CurrentField = TrackerField.Note;
    public int ColumnsPerChannel = Enum.GetValues(typeof(TrackerField)).Length; // 1 (note) + 3 (instrument) + 3 (volume) + 4 (effect) TODO: make this a const

    public double RowHeight = 15;
    public readonly int LeadInRows = 8; // number of rows before the viewport starts scrolling

    public double DigitWidth;
    public double NoteWidth;
    public double ChannelInnerPadding;
    public double ColumnWidth;

    public SynthEngine Engine { get; set; }
    public int VisibleRowCount => (int)(ActualHeight / RowHeight) + 1;

    public TrackerGrid()
    {
        DigitWidth = 7.8;
        NoteWidth = DigitWidth * 3 + ChannelInnerPadding;
        ChannelInnerPadding = 10;
        ColumnWidth =
            NoteWidth +                 // note cell
            DigitWidth * 3 +            // instrument cell (3 digits)
            DigitWidth * 2 +            // volume cell (2 digits)
            DigitWidth * 4 +            // effects cell (1 char + 3 digits)
            ChannelInnerPadding * 4;    // 4 paddings (between notes/instrs, instrs/volumes, volumes/effects, 1/2 either side)

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

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        Point colSepStart = new(0, -209);
        Point colSepEnd = new(0, 1000);
        Pen linePen = new(Brushes.ActivePatternBrush, 1);

        for (int channel = 0; channel < ChannelCount + 1; channel++)
        {
            colSepStart.X = channel * ColumnWidth;
            colSepEnd.X = channel * ColumnWidth;
            context.DrawLine(linePen, colSepStart, colSepEnd);
        }

        if (Patterns == null || Patterns.Count == 0) return;

        int globalRowOffset = 0;

        for (int i = 0; i < Patterns.Count; i++)
        {
            var brush = (i == currentPatternIndex) ? Brushes.ActivePatternBrush : Brushes.InactivePatternBrush;

            RowCount = Patterns[i].RowCount; // get number of rows in current pattern

            for (int row = 0; row < RowCount; row++)
            {
                int absoluteRow = globalRowOffset + row;
                double y = (absoluteRow - FirstVisibleRow) * RowHeight;

                if (y + (LeadInRows * RowHeight) >= 0)
                {
                    context.DrawText(
                        RenderText(
                            GetRowString(absoluteRow),
                            (i == currentPatternIndex)
                                ? (row % 4 == 0 ? Brushes.ActivePatternBrush : Brushes.LowOpacityTextBrush)
                                : (row % 4 == 0 ? Brushes.InactiveTextBrush : Brushes.LowOpacityInactiveTextBrush)
                        ),
                        new Point(-31.5, y)
                    );
                }

                for (int channel = 0; channel < ChannelCount; channel++)
                {
                    if (y + (LeadInRows * RowHeight) < 0 || y > MaxRenderHeight)
                        continue;

                    double startChannelX = channel * ColumnWidth;
                    ColumnDefinitions cols = new ColumnDefinitions(startChannelX, NoteWidth, DigitWidth, ChannelInnerPadding);
                    var cell = Patterns[i].Rows[row].Cells[channel];

                    // highlight every fourth row
                    if (row % 4 == 0)
                    {
                        context.DrawRectangle(
                            i == currentPatternIndex
                                ? Brushes.FourthRowHighlight
                                : Brushes.InactiveFourthRowHighlight,
                            null,
                            new Rect(0, y, ChannelCount * ColumnWidth, RowHeight)
                        );
                    }

                    context.DrawText(
                        RenderText(GetNoteTextToRender(cell.Note), brush), // TODO: make the --- a less bright, while keeping normal text same
                        new Point(cols.noteX, y)
                    );

                    context.DrawText(
                        RenderText(GetInstrumentTextToRender(cell.InstrumentID), brush),
                        new Point(cols.instrFirstX, y)
                    );

                    context.DrawText(
                        RenderText(GetVolumeTextToRender(cell.Velocity), brush),
                        new Point(cols.volFirstX, y)
                    );

                    //string effectText = $"{cell.EffectCode}{cell.EffectValue:D3}";
                    string effectText = $"----";
                    context.DrawText(
                        RenderText(effectText, brush),
                        new Point(cols.effectFirstX, y)
                    );

                    // highlight current cell
                    if (absoluteRow == GlobalCurrentRow && channel == CurrentChannel)
                    {
                        HighlightCurrentCell(context, startChannelX, cols.instrFirstX, cols.instrSecondX, cols.instrThirdX, cols.volFirstX,
                            cols.volSecondX, cols.effectFirstX, cols.effectSecondX, cols.effectThirdX, cols.effectFourthX, y
                        );
                    }
                }
            }

            globalRowOffset += RowCount;
        }

        // highlight current row
        context.DrawRectangle(
            Brushes.CurrentRowHighlight,
            null,
            new Rect(
                0,
                (CurrentRow - FirstVisibleRow) * RowHeight,
                ChannelCount * ColumnWidth,
                RowHeight
            ));
    }

    private void HighlightCurrentCell(DrawingContext context, double channelX, double instrFirstX, double instrSecondX,
        double instrThirdX, double volFirstX, double volSecondX, double effectFirstX, double effectSecondX,
        double effectThirdX, double effectFourthX, double y
    )
    {
        switch (CurrentField)
        {
            case TrackerField.Note:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(channelX, y, NoteWidth + ChannelInnerPadding, RowHeight)
                );
                break;

            case TrackerField.InstrumentFirstDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(instrFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.InstrumentSecondDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(instrSecondX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.InstrumentThirdDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(instrThirdX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.VolumeFirstDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(volFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.VolumeSecondDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(volSecondX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectFirstDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(effectFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectSecondDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(effectSecondX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectThirdDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(effectThirdX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectFourthDigit:
                context.DrawRectangle(
                    Brushes.CurrentCellHighlight,
                    null,
                    new Rect(effectFourthX, y, DigitWidth, RowHeight)
                );
                break;
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

    public static string GetNoteTextToRender(int note)
    {
        if (note == -1) return "---";
        return TrackerEngine.CalculateMidiNote(note);
    }

    public static string GetInstrumentTextToRender(int instrument)
    {
        if (instrument == -1) return "---";
        return instrument.ToString().PadLeft(3, '0');
    }

    public static string GetVolumeTextToRender(int volume)
    {
        if (volume == -1) return "--";
        return volume.ToString().PadLeft(2, '0');
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

    private void PlaceNote(MidiNoteValueMap note)
    {
        Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note = (int)note;
        SetNote(
            pattern: currentPatternIndex,
            row: PatternCurrentRow,
            channel: CurrentChannel,
            note: (int)note,
            bank: SelectedBank,
            instrument: SelectedInstrument == -1 ? 0 : SelectedInstrument,
            instrumentID: SelectedInstrumentID == -1 ? 0 : SelectedInstrumentID,
            volume: ProgramConstants.MaxDisplayVolume
        );
        GlobalCurrentRow++;
    }

    private void ChangeNoteInstrument(int digitIndex, int newValue)
    {
        int instrumentID = Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID;
        int updatedInstrumentID = UpdateNumberValue(
            instrumentID == -1 ? "000" : instrumentID.ToString().PadLeft(3, '0'),
            digitIndex,
            newValue
        );
        Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID = updatedInstrumentID;

        SoundFontPreset preset = PresetList.FirstOrDefault(x => x.ID == updatedInstrumentID);
        
        if (preset == null) { return; } // TODO: update to make instrument colour red if no preset found

        Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Instrument = preset.Instrument;
    }

    private void ChangeNoteVolume(int digitIndex, int newValue)
    {
        int volume = Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity;
        int updatedVolume = UpdateNumberValue(
            volume == -1 ? "00" : volume.ToString().PadLeft(2, '0'),
            digitIndex,
            newValue
        );
        Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Channel = CurrentChannel;
        Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity = updatedVolume;
    }

    private int UpdateNumberValue(string idString, int index, int newDigit)
    {
        var chars = idString.ToCharArray();
        chars[index] = (char)('0' + newDigit);
        return int.Parse(new string(chars));
    }



    private void DeleteNote(bool IsBackspace = false)
    {
        switch (CurrentField)
        {
            case TrackerField.Note:
                // remove all values in row
                SetNote(
                    pattern: currentPatternIndex,
                    row: PatternCurrentRow,
                    channel: CurrentChannel,
                    note: -1,
                    bank: -1,
                    instrument: -1,
                    instrumentID: -1,
                    volume: -1
                );
                break;
            case TrackerField.InstrumentFirstDigit:
            case TrackerField.InstrumentSecondDigit:
            case TrackerField.InstrumentThirdDigit:
                // remove instrument values only
                //SetNote(
                //    pattern: currentPatternIndex,
                //    row: PatternCurrentRow,
                //    channel: CurrentChannel,
                //    note: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note,
                //    bank: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                //    instrument: -1,
                //    instrumentID: -1,
                //    volume: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity
                //);

                // TODO: decide if i want this to even be possible, because it defaults to 000 anyway when set to -1
                break;
            case TrackerField.VolumeFirstDigit:
            case TrackerField.VolumeSecondDigit:
                // remove volume values only
                SetNote(
                    pattern: currentPatternIndex,
                    row: PatternCurrentRow,
                    channel: CurrentChannel,
                    note: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note,
                    bank: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                    instrument: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Instrument,
                    instrumentID: Engine.Tracker.Patterns[currentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID,
                    volume: -1
                );
                break;

            case TrackerField.EffectFirstDigit:
            case TrackerField.EffectSecondDigit:
            case TrackerField.EffectThirdDigit:
            case TrackerField.EffectFourthDigit:
                // remove effect values only
                break;

        }

        if (IsBackspace)
            GlobalCurrentRow--;
        else
            GlobalCurrentRow++;
    }

    private void SetNote(int pattern, int row, int channel, int note, int bank, int instrument, int instrumentID, int volume)
    {
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Channel = channel;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Note = note;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Bank = bank;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Instrument = instrument;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].InstrumentID = instrumentID;
        Engine.Tracker.Patterns[pattern].Rows[row].Cells[channel].Velocity = volume;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Patterns == null || Patterns.Count == 0) return;

        switch (CurrentField)
        {
            case TrackerField.Note:
                MidiNoteValueMap? note = GetMidiNote(e.Key);
                if (note != null)
                    PlaceNote(note.Value);
                break;

            case TrackerField.InstrumentFirstDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 0, newValue: value.Value);
                }
                break;

            case TrackerField.InstrumentSecondDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 1, newValue: value.Value);
                }
                break;

            case TrackerField.InstrumentThirdDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 2, newValue: value.Value);
                }
                break;

            case TrackerField.VolumeFirstDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                    {
                        ChangeNoteVolume(digitIndex: 0, newValue: value.Value);
                        GlobalCurrentRow++;
                    }
                }
                break;

            case TrackerField.VolumeSecondDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                    {
                        ChangeNoteVolume(digitIndex: 1, newValue: value.Value);
                        GlobalCurrentRow++;
                    }
                }
                break;

            case TrackerField.EffectFirstDigit:
                break;

            case TrackerField.EffectSecondDigit:
                break;

            case TrackerField.EffectThirdDigit:
                break;

            case TrackerField.EffectFourthDigit:
                break;
        }

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
                break;
        }

        Focus();
        InvalidateVisual();
        e.Handled = true;
    }

    private static bool IsKeyDigit(Key key)
    {
        return (key >= Key.D0 && key <= Key.D9) || (key >= Key.NumPad0 && key <= Key.NumPad9); // numpad digits
    }

    // === EVENT HANDLING === //

    public event Action<int> PlaybackStarted;
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
        PlaybackStarted?.Invoke(currentPatternIndex);
        IsPlaying = !IsPlaying;
    }

    public void SelectInstrument(int value, int id)
    {
        InstrumentSelected?.Invoke();
        SelectedInstrument = value;
        SelectedInstrumentID = id;
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

    public int GetRowsInPreviousPattern(int index)
    {
        if (index == 0) return 0;
        return Patterns[index-1].Rows.Length;
    }

    private int WrapRow(int row)
    {
        int rowsInCurrentPattern = Patterns[currentPatternIndex].Rows.Length;

        if (row >= TotalRowCount)
        {
            RowOffset = 0;
            currentPatternIndex = 0;
            return 0;
        }

        if (row < 0)
            return 0;

        if (row - RowOffset >= rowsInCurrentPattern)
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

    private int WrapColumn(int column)
    {
        // reset to first channel, first field when going out of bounds
        if (column < 0)
        {
            CurrentChannel = 0;
            CurrentField = TrackerField.Note;
            return 0;
        }

        if (column > ColumnsPerChannel * ChannelCount - 1)
        {
            return ColumnsPerChannel * ChannelCount - 1;
        }

        int change = column - CurrentColumn;

        if (change == -1) // moved back once
        {
            if (column == CurrentChannel * ColumnsPerChannel - 1)
            {
                CurrentChannel--;
                CurrentField = TrackerField.EffectFourthDigit;
            }
            else
            {
                CurrentField--;
            }
        }
        else if (change == 1) // moved forward once
        {
            if (CurrentColumn == (CurrentChannel + 1) * ColumnsPerChannel - 1)
            {
                CurrentChannel++;
                CurrentField = TrackerField.Note;
            }
            else
            {
                CurrentField++;
            }
        }

        CurrentField = (TrackerField)Math.Clamp((int)CurrentField, 0, ColumnsPerChannel - 1); // clamp to ensure within valid range

        return column;
    }

    public static MidiNoteValueMap? GetMidiNote(Key key)
    {
        if (KeyboardToMidiNote.Map.TryGetValue(key, out var note))
            return note;

        return null;
    }

    public static int? GetIntFromKey(Key key)
    {
        if (KeyboardToInt.Map.TryGetValue(key, out var value))
            return value;

        return null;
    }
}
