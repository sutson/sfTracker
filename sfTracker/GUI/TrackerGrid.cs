using sfTracker.Actions;
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

/// <summary>
/// Class which houses the main render logic for the tracker GUI.
/// </summary>
public class TrackerGrid : FrameworkElement
{
    private readonly int MaxRenderHeight = 510;
    private readonly int TrackerMarginTop = -200;
    public readonly double RowHeight = 15;
    public readonly int LeadInRows = 8; // number of "invisible rows" at the top of the tracker field
    public int VisibleRowCount => (int)(ActualHeight / RowHeight) + 1; // TODO: consider making this just 1 so scrolling keeps now playing bar in the same place
    public SynthEngine Engine { get; set; } // instance of synth engine for updating patterns

    // tracker data
    private int RowOffset = 0; // row offset value required for scrolling into different patterns
    public int CurrentRow = 0;
    public double CurrentRowPosition = 0; // used in rendering context, technically different from CurrentRow
    public int PatternCurrentRow = 0; // current row in a current pattern
    public int CurrentPatternIndex = 0;
    public int CurrentColumn = 0;
    public int CurrentChannel = 0;
    public int ChannelCount = 0;
    public int FirstVisibleRow = 0;
    public int TotalRowCount = 0;
    public int RowsPerPattern = 0;
    public int RowHighlight = 0;
    public List<Pattern> Patterns = [];
    public bool IsEditing = false;

    // instrument data
    private int SelectedBank = -1;
    private int SelectedInstrument = -1;
    private int SelectedInstrumentID = -1;
    public ObservableCollection<SoundFontPreset> PresetList = []; // preset list for SoundFont panel

    // variables for channel buttons (mute/solo)
    public ObservableCollection<TrackerColumn> ChannelStatuses;
    public double[] MuteButtonStartPositionsX;
    public double[] SoloButtonStartPositionsX;
    public double ChannelButtonStartPositionY;
    public int ChannelButtonSize = 18;

    // variables for frame selection
    public double FrameSelectStartX = -18;
    public double FrameSelectStartY = -388;
    public double FrameSelectRowWidth = 445;
    public double FrameSelectRowHeight = 20;
    public double FrameTextPadding = 5;
    public int MaxVisibleFrames = 6;
    public int FirstVisibleFrame = 0;
    public int LastVisibleFrame = 6;

    // field data
    public int FieldsPerChannel = Enum.GetValues(typeof(TrackerField)).Length; // how many selectable fields exist in a single channel (column)
    public TrackerField CurrentField = TrackerField.Note;
    
    // define widths for each column
    public double DigitWidth = 7.8;
    public double ChannelInnerPadding = 10;
    public double NoteWidth => DigitWidth * 3;
    public double ColumnWidth =>
        NoteWidth +               // note cell
        DigitWidth * 3 +          // instrument cell (3 digits)
        DigitWidth * 2 +          // volume cell (2 digits)
        DigitWidth * 3 +          // effects cell (1 char + 2 digits)
        ChannelInnerPadding * 4;  // 4 paddings (between notes/instrs, instrs/volumes, volumes/effects, 1/2 either side);
    public double RowWidth => ProgramConstants.DefaultChannelCount * ColumnWidth;

    public UndoRedoManager undoRedoManager = new UndoRedoManager();

    public TrackerGrid()
    {
        Brushes.Init();
        Focusable = true; // set focusable so key presses can be used to navigate/place notes
        IsHitTestVisible = false; // prevent filled shapes messing with scrolling
    }

    /// <summary>
    /// Main GUI render method which runs every frame.
    /// </summary>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        for (int channel = 0; channel < ChannelCount; channel++) // render column headers (Channel X [M] [S])
            DrawColumnHeader(context, channel);

        for (int channel = 0; channel < ChannelCount + 1; channel++) // render veritcal channel separation lines
            DrawColumnSeparationLine(context, channel);

        for (int pattern = FirstVisibleFrame; pattern < LastVisibleFrame; pattern++) // render frame select rows (top left of window)
            DrawFrameSelectRow(context, pattern);

        if (Patterns == null || Patterns.Count == 0) return;

        int globalRowOffset = 0; // offset updates after each pattern loop, used to get the absolute row position rather than pattern-based

        for (int i = 0; i < Patterns.Count; i++)
        {
            // render using active colour if current pattern
            var brush = (i == CurrentPatternIndex) ? Brushes.ActivePatternBrush : Brushes.InactivePatternBrush;

            for (int row = 0; row < RowsPerPattern; row++)
            {
                int absoluteRow = globalRowOffset + row;
                double y = (absoluteRow - FirstVisibleRow) * RowHeight; // calculate y-coordinate of current row

                // render row only if it should be visible
                // this renders the row numbers along the left-hand side
                if (y + (LeadInRows * RowHeight) >= 0)
                {
                    context.DrawText(
                        RenderText(
                            GetRowString(absoluteRow),
                            (i == CurrentPatternIndex)
                                ? (row % RowHighlight == 0 ? Brushes.ActivePatternBrush : Brushes.LowOpacityTextBrush)
                                : (row % RowHighlight == 0 ? Brushes.InactiveTextBrush : Brushes.LowOpacityInactiveTextBrush)
                        ),
                        new Point(-32, y)
                    );
                }

                for (int channel = 0; channel < ChannelCount; channel++)
                {
                    if (y + (LeadInRows * RowHeight) < 0 || y > MaxRenderHeight) // don't render anything outside visible region
                        continue;

                    // get starting x-coordinates of each field in the column
                    double startChannelX = channel * ColumnWidth;
                    ColumnDefinitions cols = new ColumnDefinitions(startChannelX, NoteWidth, DigitWidth, ChannelInnerPadding);
                    
                    var cell = Patterns[i].Rows[row].Cells[channel];

                    // highlight every n-th row based on RowHighlight
                    if (row % RowHighlight == 0)
                    {
                        context.DrawRectangle(
                            i == CurrentPatternIndex
                                ? Brushes.FourthRowHighlight
                                : Brushes.InactiveFourthRowHighlight,
                            null,
                            new Rect(0, y, RowWidth, RowHeight)
                        );
                    }

                    // render each field's text content (note, instrument, volume, effects)
                    context.DrawText(
                        RenderText(GetNoteTextToRender(cell.Note), brush), // TODO: make the --- a less bright, while keeping normal text same
                        new Point(cols.NoteX, y)
                    );

                    context.DrawText(
                        RenderText(GetInstrumentTextToRender(cell.InstrumentID), brush),
                        new Point(cols.InstrFirstX, y)
                    );

                    context.DrawText(
                        RenderText(GetVolumeTextToRender(cell.Velocity), brush),
                        new Point(cols.VolFirstX, y)
                    );

                    context.DrawText(
                        RenderText(GetEffectTypeTextToRender(cell.Panning), brush),
                        new Point(cols.EffectTypeX, y)
                    );

                    context.DrawText(
                        RenderText(GetEffectTextToRender(cell.Panning.Value), brush),
                        new Point(cols.EffectFirstX, y)
                    );

                    // highlight currently selected cell
                    if (absoluteRow == GlobalCurrentRow && channel == CurrentChannel)
                    {
                        HighlightCurrentCell(
                            context, startChannelX, cols.InstrFirstX, cols.InstrSecondX, cols.InstrThirdX,
                            cols.VolFirstX, cols.VolSecondX, cols.EffectTypeX, cols.EffectFirstX, cols.EffectSecondX, y
                        );
                    }
                }
            }

            globalRowOffset += RowsPerPattern; // update global row offset to next pattern
        }

        // highlight current row
        context.DrawRectangle(
            IsEditing ? Brushes.CurrentRowEditHighlight : Brushes.CurrentRowHighlight,
            null,
            new Rect(
                0,
                (CurrentRow - FirstVisibleRow) * RowHeight,
                RowWidth,
                RowHeight
            ));
    }

    /// <summary>
    /// Method to highlight the currently selected cell.
    /// </summary>
    private void HighlightCurrentCell(
        DrawingContext context, double channelX, double instrFirstX, double instrSecondX, double instrThirdX,
        double volFirstX, double volSecondX, double effectTypeX, double effectFirstX, double effectSecondX, double y
    )
    {
        Brush brush = IsEditing ? Brushes.CurrentRowEditHighlight : Brushes.CurrentCellHighlight;

        // based on currently selected field, highlight this part of the selected channel/column
        switch (CurrentField)
        {
            case TrackerField.Note:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(channelX, y, NoteWidth + ChannelInnerPadding, RowHeight)
                );
                break;

            case TrackerField.InstrumentFirstDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(instrFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.InstrumentSecondDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(instrSecondX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.InstrumentThirdDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(instrThirdX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.VolumeFirstDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(volFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.VolumeSecondDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(volSecondX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectType:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(effectTypeX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectFirstDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(effectFirstX, y, DigitWidth, RowHeight)
                );
                break;

            case TrackerField.EffectSecondDigit:
                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(effectSecondX, y, DigitWidth, RowHeight)
                );
                break;
        }
    }

    /// <summary>
    /// Method to render the column headers, i.e. "Channel X [M] [S]".
    /// </summary>
    private void DrawColumnHeader(DrawingContext context, int channel)
    {
        double headerStartX = channel * ColumnWidth;
        double headerY = TrackerMarginTop;
        double headerHeight = 80; // TODO: make this a const? it's used in the xaml
        double padding = 12;
        double buttonSize = ChannelButtonSize; // TODO: make this a const? it's used in the xaml

        // draw column headers with gradient fill
        context.DrawRectangle(
            new LinearGradientBrush(
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#373654"), 0.0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#4C4B6A"), 0.1),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#9897BA"), 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#4C4B6A"), 0.9),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#373654"), 1.0)
                ],
                new Point(0, 0),
                new Point(0, 1)
            ),
            null,
            new Rect(headerStartX, headerY, ColumnWidth, headerHeight)
        );

        double channelTextStartX = headerStartX + padding / 2;

        // draw "Channel X" text (note ToString("X") converts numbers to hexadecimal so each is the same width)
        FormattedText channelText = RenderText($"Channel {(channel + 1).ToString("X")}", Brushes.Black, 13, FontWeights.DemiBold);
        context.DrawText(
            channelText,
            new Point(
                channelTextStartX,
                headerY + (headerHeight - channelText.Height) / 2
            )
        );

        // define starting x and y positions for the mute button
        double muteButtonX = channelTextStartX + channelText.Width + padding / 2;
        double soloButtonX = muteButtonX + buttonSize + padding / 2;
        double buttonY = headerY + (headerHeight - buttonSize) / 2;

        MuteButtonStartPositionsX[channel] = muteButtonX;
        SoloButtonStartPositionsX[channel] = soloButtonX;
        ChannelButtonStartPositionY = buttonY;
        
        // mute button - create container and text, then draw it
        DrawChannelButton(context, "M", muteButtonX, buttonY, buttonSize, ChannelStatuses[channel].IsMuted);
        
        // solo button - draw based on placement of mute button
        DrawChannelButton(context, "S", soloButtonX, buttonY, buttonSize, ChannelStatuses[channel].IsSolo);
    }

    /// <summary>
    /// Method to render a button (mute/solo) inside the column header.
    /// </summary>
    private void DrawChannelButton(DrawingContext context, string value, double startX, double startY, double size, bool isSelected)
    {
        // create rectangle and text render
        Rect rect = new Rect(startX, startY, size, size);
        FormattedText text = RenderText(value, Brushes.Black, 13, FontWeights.DemiBold, "Segoe UI");

        if (isSelected) // highlight text in red if it is clicked
            text.SetForegroundBrush(Brushes.Red);

        // draw rectangle and text
        context.DrawRectangle(
                Brushes.ChannelButtonBackground, // change text colour based on background
                new Pen(Brushes.ChannelButtonOutline, 2),
                rect
            );
        context.DrawText(
            text,
            new Point(
                rect.X + (rect.Width - text.Width) / 2,  // centre text horizontally
                rect.Y + (rect.Height - text.Height) / 2 // centre text vertically
            )
        );
    }

    /// <summary>
    /// Method to render the vertical separation lines between each channel/column.
    /// </summary>
    private void DrawColumnSeparationLine(DrawingContext context, int channel)
    {
        Point colSepStart = new Point(0, TrackerMarginTop);
        Point colSepEnd = new Point(0, 1000); // 1000 is arbitrary, just needs to extend far enough
        Pen linePen = new Pen(Brushes.ActivePatternBrush, 1);

        colSepStart.X = channel * ColumnWidth;
        colSepEnd.X = channel * ColumnWidth;
        context.DrawLine(linePen, colSepStart, colSepEnd);
    }

    /// <summary>
    /// Method to render the frame selection rows in the top-left panel above the tracker grid.
    /// </summary>
    private void DrawFrameSelectRow(DrawingContext context, int pattern)
    {
        // draw row at correct position, based on pattern index
        context.DrawRectangle(
            pattern == CurrentPatternIndex ? Brushes.CurrentFrameHighlight : null, // highlight selected frame/pattern
            null,
            new Rect(
                FrameSelectStartX,
                FrameSelectStartY + (FrameSelectRowHeight * (pattern - FirstVisibleFrame)),
                FrameSelectRowWidth,
                FrameSelectRowHeight
            )
        );

        // render "Pattern X" text
        context.DrawText(
            RenderText(
                $"Pattern {pattern + 1}",
                Brushes.OffWhite,
                weight: pattern == CurrentPatternIndex ? FontWeights.Bold : FontWeights.Normal,
                font: "Segou UI"
            ),
            new Point(
                FrameSelectStartX + FrameTextPadding,
                FrameSelectStartY + (FrameSelectRowHeight * (pattern - FirstVisibleFrame)) + FrameTextPadding / 2
            )
        );
    }

    /// <summary>
    /// General method to render text element.
    /// </summary>
    public FormattedText RenderText(string noteText, SolidColorBrush brush, int size = 14, FontWeight weight = default, string font = "Consolas")
    {
        FormattedText text = 
            new FormattedText(
                noteText,                                   // text to be rendered
                CultureInfo.InvariantCulture,               // InvariantCulture ensures consistent text regardless of system culture 
                FlowDirection.LeftToRight,                  // write from left to right
                new Typeface(font),                         // font type (default is Consolas, a nice monospace font)
                size,                                       // font size
                brush,                                      // text colour
                VisualTreeHelper.GetDpi(this).PixelsPerDip  // prevent blurry text on high DPI monitors
            );

        text.SetFontWeight(weight);

        return text;
    }

    public static string GetNoteTextToRender(int note)
    {
        if (note == -1) return "---";
        if (note == ProgramConstants.StopNote) return new string('━', 3); // thick bar for "stop" note
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

    public static string GetEffectTypeTextToRender(PanEffect effect)
    {
        if (effect.Direction == null) return "-";
        return effect.Direction == EffectType.PanningLeft ? "L" : "R";
    }

    public static string GetEffectTextToRender(int value)
    {
        if (value == -1) return "--";
        return value.ToString().PadLeft(2, '0');
    }

    /// <summary>
    /// Method to get the row number value which is shown on the left of the tracker grid.
    /// </summary>
    private string GetRowString(int absoluteRow)
    {
        return (absoluteRow % RowsPerPattern).ToString().PadLeft(3, '0');
    }

    /// <summary>
    /// Method to handle key press when tracker grid is focused.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Patterns == null || Patterns.Count == 0) return;

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
                HandleDeleteField();
                break;

            case Key.Oem3: // '@ key
                if (!IsEditing) { return; }
                HandlePitchChange(PitchChange.DecreaseSemitone);
                break;

            case Key.Oem7: // #~ key
                if (!IsEditing) { return; }
                HandlePitchChange(PitchChange.IncreaseSemitone);
                break;

            case Key.Oem4: // [{ key
                if (!IsEditing) { return; }
                HandlePitchChange(PitchChange.DecreaseOctave);
                break;

            case Key.Oem6: // ]} key
                if (!IsEditing) { return; }
                HandlePitchChange(PitchChange.IncreaseOctave);
                break;

            case Key.Back:
                HandleDeleteField(IsBackspace: true);
                break;

            case Key.Enter:
                StartPlayback();
                break;

            case Key.Z:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    undoRedoManager.Undo();
                    return;
                }
                break;

            case Key.Y:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    undoRedoManager.Redo();
                    return;
                }
                break;
        }

        if (!IsEditing) { return; }

        switch (CurrentField)
        {
            case TrackerField.Note:
                if (GetIntFromKey(e.Key) == (int)Keybinds.StopNote) // handle placing "stop" note
                {
                    UpdateNote(
                        note: ProgramConstants.StopNote,
                        bank: -1,
                        instrument: -1,
                        instrumentID: -1,
                        velocity: -1,
                        panning: ProgramConstants.DefaultPanEffect
                    );

                    GlobalCurrentRow++; // move down after note is placed
                    break;
                }

                MidiNoteValueMap? note = GetMidiNote(e.Key); // handle placing normal note
                if (note != null)
                {
                    UpdateNote(
                        note: (int)note.Value - 12, // TODO: not sure if this will break something but sf2 pitches are weird
                        bank: SelectedBank,
                        instrument: SelectedInstrument == -1 ? 0 : SelectedInstrument,
                        instrumentID: SelectedInstrumentID == -1 ? 0 : SelectedInstrumentID,
                        velocity: ProgramConstants.MaxDisplayVolume,
                        panning: ProgramConstants.DefaultPanEffect
                    );

                    GlobalCurrentRow++;
                }
                break;

            case TrackerField.InstrumentFirstDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 0, newValue: value.Value); // update first digit
                }
                break;

            case TrackerField.InstrumentSecondDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 1, newValue: value.Value); // update second digit
                }
                break;

            case TrackerField.InstrumentThirdDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteInstrument(digitIndex: 2, newValue: value.Value); // update third digit
                }
                break;

            case TrackerField.VolumeFirstDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteVolume(digitIndex: 0, newValue: value.Value); // update first digit
                }
                break;

            case TrackerField.VolumeSecondDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNoteVolume(digitIndex: 1, newValue: value.Value); // update second digit
                }
                break;
            
            case TrackerField.EffectType:
                if (IsKeyDigit(e.Key)) { return; } // don't allow digits in the effect type field

                if (e.Key == Key.L)
                    HandleEffectTypeChange(new PanEffect(direction: EffectType.PanningLeft, value: 0)); // insantiate Left panning
                else if (e.Key == Key.R)
                    HandleEffectTypeChange(new PanEffect(direction: EffectType.PanningRight, value: 0)); // insantiate Rights panning
                break;

            case TrackerField.EffectFirstDigit:
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null && value * 10 <= ProgramConstants.MaxDisplayPanning) // do not allow changes over max panning display value
                        ChangeNotePanning(digitIndex: 0, newValue: value.Value); // update first digit
                }
                break;

            case TrackerField.EffectSecondDigit: // TODO: cap value properly at 50
                if (IsKeyDigit(e.Key))
                {
                    int? value = GetIntFromKey(e.Key);
                    if (value != null)
                        ChangeNotePanning(digitIndex: 1, newValue: value.Value); // update second digit
                }
                break;
        }

        Focus();
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// Method to perform a Cell update.
    /// </summary>
    private void UpdateNote(int note, int bank, int instrument, int instrumentID, int velocity, PanEffect panning)
    {
        // execute cell update action in the undo/redo manager to add it to the appropriate stack
        undoRedoManager.Execute
        (
            new UpdateCellAction(
                currentPattern: CurrentPatternIndex,
                row: PatternCurrentRow,
                channel: CurrentChannel,
                patterns: Patterns,
                newCell: new Cell
                {
                    Channel = CurrentChannel,
                    Note = note,
                    Bank = bank,
                    Instrument = instrument,
                    InstrumentID = instrumentID,
                    Velocity = velocity,
                    Panning = panning
                }
            )
        );
    }

    /// <summary>
    /// Method to perform a pitch change action on the note associated with a Cell.
    /// </summary>
    private void TransposeNote(int note)
    {
        // execute cell update action in the undo/redo manager to add it to the appropriate stack
        undoRedoManager.Execute
        (
            new UpdateCellAction(
                currentPattern: CurrentPatternIndex,
                row: PatternCurrentRow,
                channel: CurrentChannel,
                patterns: Patterns,
                newCell: new Cell
                {
                    Channel = CurrentChannel,
                    Note = note,
                    Bank = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                    Instrument = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Instrument,
                    InstrumentID = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID,
                    Velocity = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity,
                    Panning = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning
                }
            )
        );
    }

    /// <summary>
    /// Method to perform an instrument change action on the note associated with a Cell.
    /// </summary>
    private void UpdateInstrument(int instrument, int instrumentID)
    {
        // execute cell update action in the undo/redo manager to add it to the appropriate stack
        undoRedoManager.Execute
        (
            new UpdateCellAction(
                currentPattern: CurrentPatternIndex,
                row: PatternCurrentRow,
                channel: CurrentChannel,
                patterns: Patterns,
                newCell: new Cell
                {
                    Channel = CurrentChannel,
                    Note = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note,
                    Bank = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                    Instrument = instrument,
                    InstrumentID = instrumentID,
                    Velocity = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity,
                    Panning = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning
                }
            )
        );
    }

    /// <summary>
    /// Method to perform a pitch volume (velocity) change action on the note associated with a Cell.
    /// </summary>
    private void UpdateVolume(int velocity)
    {
        // execute cell update action in the undo/redo manager to add it to the appropriate stack
        undoRedoManager.Execute
        (
            new UpdateCellAction(
                currentPattern: CurrentPatternIndex,
                row: PatternCurrentRow,
                channel: CurrentChannel,
                patterns: Patterns,
                newCell: new Cell
                {
                    Channel = CurrentChannel,
                    Note = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note,
                    Bank = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                    Instrument = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Instrument,
                    InstrumentID = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID,
                    Velocity = velocity,
                    Panning = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning
                }
            )
        );
    }

    /// <summary>
    /// Method to perform a panning change action on the note associated with a Cell.
    /// </summary>
    private void UpdatePanning(PanEffect panning)
    {
        // execute cell update action in the undo/redo manager to add it to the appropriate stack
        undoRedoManager.Execute
        (
            new UpdateCellAction(
                currentPattern: CurrentPatternIndex,
                row: PatternCurrentRow,
                channel: CurrentChannel,
                patterns: Patterns,
                newCell: new Cell
                {
                    Channel = CurrentChannel,
                    Note = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note,
                    Bank = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Bank,
                    Instrument = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Instrument,
                    InstrumentID = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID,
                    Velocity = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity,
                    Panning = panning
                }
            )
        );
    }

    /// <summary>
    /// Method to handle deletion of a value in each field of a channel/column.
    /// </summary>
    private void HandleDeleteField(bool IsBackspace = false)
    {
        switch (CurrentField)
        {
            case TrackerField.Note:
                // remove all values in row
                UpdateNote(
                    note: -1,
                    bank: -1,
                    instrument: -1,
                    instrumentID: -1,
                    velocity: -1,
                    panning: ProgramConstants.DefaultPanEffect
                );
                break;

            case TrackerField.InstrumentFirstDigit:
            case TrackerField.InstrumentSecondDigit:
            case TrackerField.InstrumentThirdDigit:
                return; // don't allow deletion of instrument (defaults to 0 anyway)

            case TrackerField.VolumeFirstDigit:
            case TrackerField.VolumeSecondDigit:
                UpdateVolume(velocity: -1); // remove volume values only
                break;

            case TrackerField.EffectType:
            case TrackerField.EffectFirstDigit:
            case TrackerField.EffectSecondDigit:
                UpdatePanning(ProgramConstants.DefaultPanEffect); // remove effect type and digits
                break;
        }

        if (IsBackspace)
            GlobalCurrentRow--; // move up if backspace used to delete
        else
            GlobalCurrentRow++;
    }

    /// <summary>
    /// Method to handle pitch change of a note.
    /// </summary>
    private void HandlePitchChange(PitchChange change)
    {
        // do nothing if note is empty or stop note
        if (Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note < 0) { return; }

        // update note pitch based on change input (either semitone or octave change)
        // ensure it is clamped within the region of valid MIDI notes
        int updatedValue = Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note + (int)change;
        if (updatedValue < ProgramConstants.MinMidiNoteValue || updatedValue > ProgramConstants.MaxMidiNoteValue) { return; }
        TransposeNote(updatedValue);
    }

    /// <summary>
    /// Method to handle pitch change of a note.
    /// </summary>
    private void HandleEffectTypeChange(PanEffect effect)
    {
        UpdatePanning(effect);
        GlobalCurrentRow++;
    }

    /// <summary>
    /// Method to handle instrument change of a note.
    /// </summary>
    private void ChangeNoteInstrument(int digitIndex, int newValue)
    {
        // just move to next row if note is empty or stop note
        if (Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note < 0) { GlobalCurrentRow++; return; }

        // update instrument ID based on display value, needed because the user can change any of the three digits
        // so must be converted to 3-digit number then parsed back to an integer
        int instrumentID = Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].InstrumentID;
        int updatedInstrumentID = UpdateNumberValue(
            instrumentID == -1 ? "000" : instrumentID.ToString().PadLeft(3, '0'),
            digitIndex,
            newValue
        );

        // find the preset (if it exists) and update the instrument
        SoundFontPreset preset = GetSoundFontPresetFromID(updatedInstrumentID);
        if (preset == null) { return; } // TODO: update to make instrument colour red if no preset found
        UpdateInstrument(instrument: preset.Instrument, instrumentID: preset.ID);
        GlobalCurrentRow++;
    }

    /// <summary>
    /// Method to recalculate which preset should be used when changing SoundFonts.
    /// This is required because otherwise the displayed IDs don't necessarily match with the preset when a new SoundFont is loaded in.
    /// </summary>
    public void RecalculatePresets()
    {
        for (int pattern = 0; pattern < Patterns.Count; pattern++)
            for (int row = 0; row < Patterns[pattern].Rows.Length; row++)
                for (int cell = 0; cell < Patterns[pattern].Rows[row].Cells.Length; cell++)
                {
                    SoundFontPreset preset = GetSoundFontPresetFromID(Patterns[pattern].Rows[row].Cells[cell].InstrumentID);
                    if (preset == null) { continue; } // TODO: update to make instrument colour red if no preset found
                    Engine.Tracker.Patterns[pattern].Rows[row].Cells[cell].Instrument = preset.Instrument;
                    Engine.Tracker.Patterns[pattern].Rows[row].Cells[cell].InstrumentID = preset.ID;
                }
    }

    private SoundFontPreset GetSoundFontPresetFromID(int id)
    {
        return PresetList.FirstOrDefault(x => x.ID == id);
    }

    /// <summary>
    /// Method to handle volume (velocity) change of a note.
    /// </summary>
    private void ChangeNoteVolume(int digitIndex, int newValue)
    {
        // do nothing if note is empty or stop note
        if (Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Note == ProgramConstants.StopNote) { return; }

        // update volume based on display value, needed because the user can change any of the two digits
        // so must be converted to 2-digit number then parsed back to an integer
        int volume = Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Velocity;
        int updatedVolume = UpdateNumberValue(
            volume == -1 ? "00" : volume.ToString().PadLeft(2, '0'),
            digitIndex,
            newValue
        );

        UpdateVolume(updatedVolume);
        GlobalCurrentRow++;
    }

    /// <summary>
    /// Method to handle panning change of a note.
    /// </summary>
    private void ChangeNotePanning(int digitIndex, int newValue)
    {
        // do nothing if there is no effect being targeted (L or R not present in the effect type field)
        if (Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning.Direction == null) { return; }

        // update panning value based on display value, needed because the user can change any of the two digits
        // so must be converted to 2-digit number then parsed back to an integer
        int panningValue = Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning.Value;
        int updatedPanningValue = UpdateNumberValue(
            panningValue == 0 ? "00" : panningValue.ToString().PadLeft(2, '0'),
            digitIndex,
            newValue
        );

        // create new panning object and update panning data
        PanEffect updatedPanning = new PanEffect(
            Engine.Tracker.Patterns[CurrentPatternIndex].Rows[PatternCurrentRow].Cells[CurrentChannel].Panning.Direction,
            updatedPanningValue
        );

        UpdatePanning(updatedPanning);
        GlobalCurrentRow++;
    }

    /// <summary>
    /// Method to update a display value by changing a specific index to the desired digit.
    /// </summary>
    private static int UpdateNumberValue(string numberString, int index, int newDigit)
    {
        var chars = numberString.ToCharArray();
        chars[index] = (char)('0' + newDigit);
        return int.Parse(new string(chars));
    }

    // event handling
    public event Action<int> PlaybackStarted;
    public event Action InstrumentSelected;
    public event Action BankSelected;
    public event Action<double> VerticalScrollbarValueChanged;
    public event Action<double> HorizontalScrollbarValueChanged;
    public event Action<double> FrameVerticalScrollbarValueChanged;
    public event Action<int, int> AdvancedRow;
    public event Action<int, int> AdvancedColumn;
    public event Action<int> RowChanged;
    public event Action<int> ColumnChanged;
    public event Action<int> PatternChanged;

    public void StartPlayback()
    {
        PlaybackStarted?.Invoke(CurrentPatternIndex);
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
    }

    public void SetHorizontalScrollbarValue(double value)
    {
        HorizontalScrollbarValueChanged?.Invoke(value);
    }

    public void SetFrameVerticalScrollbarValue(double value)
    {
        FrameVerticalScrollbarValueChanged?.Invoke(value);
    }

    public void SetCurrentRow(int row)
    {
        CurrentRowPosition = row;
        InvalidateVisual();
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

            CurrentRow = WrapRow(value); // ensure row is wrapped when reaching end of pattern

            EnsureVisible(); // make sure manual scrolling keeps highlighted row visible
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

            CurrentColumn = WrapColumn(value); // ensure column is wrapped when reaching end of fields

            InvalidateVisual();
            ColumnChanged?.Invoke(CurrentColumn);
        }
    }

    /// <summary>
    /// Method to make sure current row is visible when scrolling.
    /// </summary>
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

    /// <summary>
    /// Method to wrap the row value to update pattern index and prevent out of bounds issues.
    /// </summary>
    private int WrapRow(int row)
    {
        if (row < 0 || row >= TotalRowCount) // scrolling outside of start and end bounds resets to first row
        {
            ResetToFirstRow();
            return 0;
        }

        else if (row - RowOffset >= RowsPerPattern) // scrolling into next pattern
        {
            RowOffset += RowsPerPattern; // update row offset as now in the next pattern
            CurrentPatternIndex++;
            PatternChanged?.Invoke(CurrentPatternIndex); // update selected frame in top-left window
        }

        else if (row < RowOffset) // scrolling into previous pattern
        {
            RowOffset -= RowsPerPattern; // update row offset as now in the previous pattern
            CurrentPatternIndex--;
            PatternChanged?.Invoke(CurrentPatternIndex); // update selected frame in top-left window
        }

        PatternCurrentRow = row - RowOffset; // update current row within current pattern

        return row;
    }

    /// <summary>
    /// Method to reset the tracker to the first row.
    /// </summary>
    public void ResetToFirstRow()
    {
        RowOffset = 0;
        CurrentPatternIndex = 0;
        PatternChanged?.Invoke(0);
    }

    /// <summary>
    /// Method to wrap the column value to update current field and prevent out of bounds issues.
    /// </summary>
    private int WrapColumn(int column)
    {
        if (column < 0) // reset to first channel, first field when going out of bounds
        {
            CurrentChannel = 0;
            CurrentField = TrackerField.Note;
            return 0;
        }

        if (column > FieldsPerChannel * ChannelCount - 1) // prevent going beyond end boundary
            return FieldsPerChannel * ChannelCount - 1;

        int change = column - CurrentColumn; // determine if column change is left (negative) or right (positive)

        if (change == -1) // moved back once
        {
            // moving backwards from first field of (n)th channel to last field of (n-1)the channel
            if (column == CurrentChannel * FieldsPerChannel - 1)
            {
                CurrentChannel--;
                CurrentField = TrackerField.EffectSecondDigit; // last field of new channel
            }
            else
            {
                CurrentField--;
            }
        }
        else if (change == 1) // moved forward once
        {
            // moving forwards from last field of (n)th channel to first field of (n+1)the channel
            if (CurrentColumn == (CurrentChannel + 1) * FieldsPerChannel - 1)
            {
                CurrentChannel++;
                CurrentField = TrackerField.Note; // first field of new channel
            }
            else
            {
                CurrentField++;
            }
        }

        CurrentField = (TrackerField)Math.Clamp((int)CurrentField, 0, FieldsPerChannel - 1); // clamp to ensure within valid range

        return column;
    }

    private static bool IsKeyDigit(Key key)
    {
        return (key >= Key.D0 && key <= Key.D9) || (key >= Key.NumPad0 && key <= Key.NumPad9); // numpad digits
    }

    public MidiNoteValueMap? GetMidiNote(Key key)
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
