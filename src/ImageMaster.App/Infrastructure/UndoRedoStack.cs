namespace ImageMaster.App.Infrastructure;

/// <summary>
/// Simple snapshot-based undo/redo stack. Each edit pushes a full copy of
/// the previous state; simple and correct for an editor with modest
/// image sizes and a bounded history, at the cost of holding several full
/// image copies in memory. A production build with very large images or
/// long histories would switch to a command/diff-based approach instead.
/// </summary>
/// <typeparam name="T">The snapshot type (an immutable/cloned state).</typeparam>
public sealed class UndoRedoStack<T>
{
    private readonly int _maxHistory;
    private readonly Stack<T> _undoStack = new();
    private readonly Stack<T> _redoStack = new();

    public UndoRedoStack(int maxHistory = 20)
    {
        if (maxHistory <= 0) throw new ArgumentOutOfRangeException(nameof(maxHistory));
        _maxHistory = maxHistory;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Records <paramref name="stateBeforeEdit"/> as the point to return to on Undo, and clears the redo history.</summary>
    public void Push(T stateBeforeEdit)
    {
        _undoStack.Push(stateBeforeEdit);
        _redoStack.Clear();

        // Bound memory use: drop the oldest entries beyond the history limit.
        if (_undoStack.Count > _maxHistory)
        {
            var trimmed = _undoStack.Take(_maxHistory).Reverse().ToArray();
            _undoStack.Clear();
            foreach (var item in trimmed)
                _undoStack.Push(item);
        }
    }

    /// <summary>Pops the last recorded state and pushes <paramref name="currentState"/> onto the redo stack.</summary>
    public T Undo(T currentState)
    {
        if (!CanUndo) throw new InvalidOperationException("Nothing to undo.");
        _redoStack.Push(currentState);
        return _undoStack.Pop();
    }

    public T Redo(T currentState)
    {
        if (!CanRedo) throw new InvalidOperationException("Nothing to redo.");
        _undoStack.Push(currentState);
        return _redoStack.Pop();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
