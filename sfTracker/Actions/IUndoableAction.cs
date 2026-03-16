namespace sfTracker.Actions
{
    /// <summary>
    /// Interface for actions which can be undone.
    /// </summary>
    public interface IUndoableAction
    {
        void Execute();
        void Undo();
    }
}
