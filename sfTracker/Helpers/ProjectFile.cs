using sfTracker.Playback;
using System.Collections.Generic;

namespace sfTracker.Helpers
{
    /// <summary>
    /// Class for creating the structure of a project for saving to file.
    /// </summary>
    public class ProjectFile
    {
        public string ProjectName { get; set; }
        public string SoundFont { get; set; }
        public int BPM { get; set; }
        public int Speed { get; set; }
        public int RowCount { get; set; }
        public int RowHighlight { get; set; }
        public List<Pattern> Patterns { get; set; } = [];
    }
}
