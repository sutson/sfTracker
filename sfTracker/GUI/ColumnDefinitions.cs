using System.Collections.Generic;

namespace sfTracker.GUI
{
    /// <summary>
    /// Class which generates x-coordinates for a given channel/column based on a starting reference point.
    /// </summary>
    public class ColumnDefinitions
    {
        public double NoteX { get; private set; }
        public double InstrFirstX { get; private set; }
        public double InstrSecondX { get; private set; }
        public double InstrThirdX { get; private set; }
        public double VolFirstX { get; private set; }
        public double VolSecondX { get; private set; }
        public double EffectTypeX { get; private set; }
        public double EffectFirstX { get; private set; }
        public double EffectSecondX { get; private set; }

        private double NoteWidth { get; set; }
        private double DigitWidth { get; set; }
        private double Padding { get; set; }

        public ColumnDefinitions(double startX, double noteWidth, double digitWidth, double padding)
        {
            // define layout of note, instrument, volume and effects
            NoteX = startX + padding / 2;

            InstrFirstX = NoteX + noteWidth + padding;
            InstrSecondX = InstrFirstX + digitWidth;
            InstrThirdX = InstrSecondX + digitWidth;

            VolFirstX = InstrThirdX + digitWidth + padding;
            VolSecondX = VolFirstX + digitWidth;

            EffectTypeX = VolSecondX + digitWidth + padding;
            EffectFirstX = EffectTypeX + digitWidth;
            EffectSecondX = EffectFirstX + digitWidth;

            // set note, digit and padding data
            NoteWidth = noteWidth;
            DigitWidth = digitWidth;
            Padding = padding;
        }

        /// <summary>
        /// Method to get the starting x-coordinates of each field in a column/channel.
        /// </summary>
        public List<double> GetColumnCoordinates()
        {
            return [
                NoteX - Padding/2,
                InstrFirstX, InstrSecondX, InstrThirdX,
                VolFirstX, VolSecondX,
                EffectTypeX, EffectFirstX, EffectSecondX
            ];
        }

        /// <summary>
        /// Method to get the width of each field in a column/channel.
        /// </summary>
        public List<double> GetColumnWidths()
        {
            return [
                NoteWidth,                          // note 
                DigitWidth, DigitWidth, DigitWidth, // Instrument (3 digits)
                DigitWidth, DigitWidth,             // volume (2 digits)
                DigitWidth, DigitWidth, DigitWidth  // effects (1 type char + 2 digits)
            ];
        }
    }
}
