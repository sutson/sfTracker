using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace sfTracker.Playback
{
    /// <summary>
    /// Class which models a row which contains a group of <c>Cell</c>s
    /// </summary>
    public class Row
    {
        public Cell[] Cells { get; set; }

        [JsonConstructor] // need this for saving/loading files, Json requires the constructor to match the fields exactly
        public Row(Cell[] cells)
        {
            Cells = cells;
        }

        public Row(int channels) 
        {
            Cells = new Cell[channels];
            for (int i = 0; i < channels; i++)
            {
                Cells[i] = new Cell();
            }
        }
    }
}
