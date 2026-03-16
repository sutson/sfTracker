using sfTracker.Controls;

namespace sfTracker.Playback
{
    /// <summary>
    /// Class for facilitating <c>Cell</c> note panning.
    /// </summary>
    public class PanEffect(EffectType? direction, int value)
    {
        public EffectType? Direction { get; set; } = direction;
        public int Value { get; set; } = value;
    }
}