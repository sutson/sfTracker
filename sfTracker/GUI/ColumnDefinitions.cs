using System.Collections.Generic;

namespace sfTracker.GUI
{
    public class ColumnDefinitions
    {
        public double noteX { get; private set; }
        public double instrFirstX { get; private set; }
        public double instrSecondX { get; private set; }
        public double instrThirdX { get; private set; }
        public double volFirstX { get; private set; }
        public double volSecondX { get; private set; }
        public double effectFirstX { get; private set; }
        public double effectSecondX { get; private set; }
        public double effectThirdX { get; private set; }
        public double effectFourthX { get; private set; }

        private double NoteWidth { get; set; }
        private double DigitWidth { get; set; }
        private double Padding { get; set; }


        public ColumnDefinitions(double startX, double noteWidth, double digitWidth, double padding)
        {
            noteX = startX + padding / 2;

            instrFirstX = noteX + noteWidth + padding;
            instrSecondX = instrFirstX + digitWidth;
            instrThirdX = instrSecondX + digitWidth;

            volFirstX = instrThirdX + digitWidth + padding;
            volSecondX = volFirstX + digitWidth;

            effectFirstX = volSecondX + digitWidth + padding;
            effectSecondX = effectFirstX + digitWidth;
            effectThirdX = effectSecondX + digitWidth;
            effectFourthX = effectThirdX + digitWidth;

            NoteWidth = noteWidth;
            DigitWidth = digitWidth;
            Padding = padding;
        }

        public List<double> GetColumnCoordinates()
        {
            return [
                noteX - Padding/2, instrFirstX, instrSecondX, instrThirdX, volFirstX,
                volSecondX, effectFirstX, effectSecondX, effectThirdX, effectFourthX
            ];
        }

        public List<double> GetColumnWidths()
        {
            return [
                NoteWidth, DigitWidth, DigitWidth, DigitWidth, DigitWidth,
                DigitWidth, DigitWidth, DigitWidth, DigitWidth, DigitWidth
            ];
        }
    }
}
