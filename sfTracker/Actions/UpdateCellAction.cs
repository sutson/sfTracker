using sfTracker.Playback;
using System.Collections.Generic;

namespace sfTracker.Actions
{
    public class UpdateCellAction(int currentPattern, int row, int channel, List<Pattern> patterns, Cell newCell) : IUndoableAction
    {
        private readonly int currentPattern = currentPattern;
        private readonly int row = row;
        private readonly int channel = channel;

        private readonly List<Pattern> patterns = patterns;

        private readonly Cell newCell = newCell;
        private readonly Cell oldCell = patterns[currentPattern].Rows[row].Cells[channel];

        public void Execute()
        {
            patterns[currentPattern].Rows[row].Cells[channel] = newCell;
        }

        public void Undo()
        {
            patterns[currentPattern].Rows[row].Cells[channel] = oldCell;
        }
    }
}
