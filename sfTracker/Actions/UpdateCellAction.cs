using sfTracker.Playback;
using System.Collections.Generic;

namespace sfTracker.Actions
{
    /// <summary>
    /// Class for performing a <c>Cell</c> update action.
    /// </summary>
    public class UpdateCellAction(int currentPattern, int row, int channel, List<Pattern> patterns, Cell newCell) : IUndoableAction
    {
        private readonly int currentPattern = currentPattern;
        private readonly int row = row;
        private readonly int channel = channel;

        private readonly List<Pattern> patterns = patterns;

        private readonly Cell newCell = newCell;
        private readonly Cell oldCell = patterns[currentPattern].Rows[row].Cells[channel]; // store current Cell data for undoing

        public void Execute()
        {
            patterns[currentPattern].Rows[row].Cells[channel] = newCell; // update to new Cell data
        }

        public void Undo()
        {
            patterns[currentPattern].Rows[row].Cells[channel] = oldCell; // update back to old Cell data when undoing
        }
    }
}
