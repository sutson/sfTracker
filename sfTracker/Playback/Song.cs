namespace sfTracker.Playback
{
    /// <summary>
    /// Class which models a song which contains a group of <c>Pattern</c>s
    /// </summary>
    public class Song
    {
        public int PatternCount => Patterns.Length;
        public Pattern[] Patterns { get; set;  }

        public Song(int patternCount, int rowCount, int channels)
        {
            Patterns = new Pattern[patternCount];
            for (int i = 0; i < patternCount; i++)
                Patterns[i] = new Pattern(rowCount, channels);
        }
    }
}
