namespace sfTracker.Tracker
{
    /// <summary>
    /// Class which models an active voice.
    /// This is used to determine which note is playing in a given channel (column) at a given time.
    /// </summary>
    public class Voice(int note, int instrument, int velocity)
    {
        public int Note { get; } = note;
        public int Instrument { get; private set; } = instrument;
        public int Velocity { get; private set; } = velocity;
    }
}