using sfTracker.Playback;

namespace sfTracker.Common
{
    /// <summary>
    /// Class for storing some constants which are used in various different classes.
    /// </summary>
    public static class ProgramConstants
    {
        public const int MaxVolume = 127;
        public const int MaxDisplayVolume = 99;

        public const int StopNote = -100;

        public const int MinMidiNoteValue = 12; // C-0
        public const int MaxMidiNoteValue = 107; // B-7

        public const int MaxDisplayPanning = 50;
        public const int DefaultPanning = 63; // halfway between 0 and 127
        public static PanEffect DefaultPanEffect = new PanEffect(direction: null, value: -1);

        public const int MinBPM = 32;
        public const int MaxBPM = 255;

        public const int MinSpeed = 1;
        public const int MaxSpeed = 31;

        public const int MinRowCount = 4;
        public const int MaxRowCount = 256;

        public const int MinRowHighlight = 1;
        public const int MaxRowHighlight = 32;

        public const string DefaultSoundFont = "Kirby's_Dream_Land_3.sf2"; // TODO: find a way to make this not required

        public const int DefaultBPM = 120;
        public const int DefaultSpeed = 6;
        public const int DefaultRowCount = 32;
        public const int DefaultRowHighlight = 4;
        public const int DefaultChannelCount = 12;
    }
}
