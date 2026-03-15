using System.Collections.Generic;

namespace sfTracker.Actions
{
    public class UndoRedoManager
    {
        private readonly Stack<IUndoableAction> UndoStack = new Stack<IUndoableAction>();
        private readonly Stack<IUndoableAction> RedoStack = new Stack<IUndoableAction>();

        public void Execute(IUndoableAction action)
        {
            action.Execute();
            UndoStack.Push(action);
            RedoStack.Clear();
        }

        public void Undo()
        {
            if (UndoStack.Count == 0) return;

            var action = UndoStack.Pop();
            action.Undo();
            RedoStack.Push(action);
        }
        public void Redo()
        {
            if (RedoStack.Count == 0) return;

            var action = RedoStack.Pop();
            action.Execute();
            UndoStack.Push(action);
        }

    }
}
