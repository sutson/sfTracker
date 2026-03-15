namespace sfTracker.Actions
{
    public interface IUndoableAction
    {
        void Execute();
        void Undo();
    }
}
