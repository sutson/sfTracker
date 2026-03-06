using sfTracker.Common;

namespace sfTracker.Playback
{
    /// <summary>
    /// Class which models a single <c>Cell</c> in a row
    /// </summary>
    public class Cell
    {
        public int Channel { get; set; } = -1;    // column corresponding to cell
        public int Note { get; set; } = -1;       // pitch of note
        public int Bank { get; set; } = -1;       // bank number in SoundFont
        public int Instrument { get; set; } = -1; // instrument number in SoundFont
        public int InstrumentID { get; set; } = -1; // instrument ID for displaying on the tracker grid
        public int Velocity { get; set; } = -1;  // volume of the cell
        public PanEffect Panning { get; set; } = ProgramConstants.DefaultPanEffect;  // panning of the cell
    }
}