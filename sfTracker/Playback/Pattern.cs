namespace sfTracker.Playback
{
    /// <summary>
    /// Class which models a pattern which contains a group of <c>Row</c>s with <c>Cell</c>s
    /// </summary>
    public class Pattern
    {
        public int RowCount => Rows.Length;
        public Row[] Rows { get; }

        public Pattern(int rowCount, int channels)
        {
            Rows = new Row[rowCount];
            for (int i = 0; i < rowCount; i++)
                Rows[i] = new Row(channels);
        }
    }
}
