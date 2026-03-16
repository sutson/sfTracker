using System.Collections.Generic;

namespace sfTracker.Actions
{
    /// <summary>
    /// Class for implementing simple undo/redo logic.
    /// </summary>
    public class UndoRedoManager
    {
        private readonly Stack<IUndoableAction> UndoStack = new Stack<IUndoableAction>();
        private readonly Stack<IUndoableAction> RedoStack = new Stack<IUndoableAction>();

        public void Execute(IUndoableAction action)
        {
            action.Execute();
            UndoStack.Push(action); // add action to the undo stack
            RedoStack.Clear();
        }

        public void Undo()
        {
            if (UndoStack.Count == 0) return; // do nothing if stack is empty

            var action = UndoStack.Pop();
            action.Undo();
            RedoStack.Push(action); // add action to the redo stack to make undo action reversible
        }
        public void Redo()
        {
            if (RedoStack.Count == 0) return; // do nothing if stack is empty

            var action = RedoStack.Pop();
            action.Execute();
            UndoStack.Push(action); // add action to the undo stack to make redo action reversible
        }

    }
}
