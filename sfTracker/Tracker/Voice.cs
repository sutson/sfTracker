using System.Windows.Media.Animation;

namespace sfTracker.Tracker
{
    /// <summary>
    /// Class which models an active voice.
    /// This is used to determine which note is playing in a given column/channel at a given time.
    /// </summary>
    public class Voice(int note, int bank, int instrument, int velocity)
    {
        public int Note { get; } = note;
        public int Bank { get; private set; } = bank;
        public int Instrument { get; private set; } = instrument;
        public int Velocity { get; private set; } = velocity;
    }
}