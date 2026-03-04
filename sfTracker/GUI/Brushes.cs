using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace sfTracker.GUI
{
    public class Brushes
    {
        public static SolidColorBrush FourthRowHighlight;
        public static SolidColorBrush InactiveFourthRowHighlight;
        public static SolidColorBrush CurrentCellHighlight;
        public static SolidColorBrush CurrentRowHighlight;
        public static SolidColorBrush ActivePatternBrush;
        public static SolidColorBrush InactivePatternBrush;
        public static SolidColorBrush LowOpacityTextBrush;
        public static SolidColorBrush InactiveTextBrush;
        public static SolidColorBrush LowOpacityInactiveTextBrush;

        public static void Init()
        {
            FourthRowHighlight = new SolidColorBrush(Color.FromArgb(5, 255, 255, 255));
            FourthRowHighlight.Freeze();

            InactiveFourthRowHighlight = new SolidColorBrush(Color.FromArgb(3, 255, 255, 255));
            InactiveFourthRowHighlight.Freeze();

            CurrentCellHighlight = new SolidColorBrush(Color.FromArgb(120, 0, 120, 215));
            CurrentCellHighlight.Freeze();

            CurrentRowHighlight = new SolidColorBrush(Color.FromArgb(50, 10, 255, 255));
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
        }
        
    }
}
