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
        public static SolidColorBrush ChannelButtonBackground;
        public static SolidColorBrush ChannelButtonOutline;
        public static SolidColorBrush Black;
        public static SolidColorBrush Red;
        public static SolidColorBrush OffWhite;
        public static SolidColorBrush CurrentFrameHighlight;
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

            ChannelButtonBackground = new SolidColorBrush(Color.FromArgb(255, 196, 195, 219));
            ChannelButtonBackground.Freeze();
            
            ChannelButtonOutline = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
            ChannelButtonOutline.Freeze();

            Black = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            Black.Freeze();

            Red = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
            Red.Freeze();

            OffWhite = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            OffWhite.Freeze();

            CurrentFrameHighlight = new SolidColorBrush(Color.FromArgb(255, 60, 59, 92));
            CurrentFrameHighlight.Freeze();
        }
        
    }
}
