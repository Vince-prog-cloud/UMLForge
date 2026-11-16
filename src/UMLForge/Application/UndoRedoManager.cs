namespace UmlForge;

public class UndoRedoManager
{
    readonly Stack<string> undo = new(), redo = new();
    string pending;
    readonly int maxHistory;

    public UndoRedoManager(int maxHistory = 80) => this.maxHistory = maxHistory;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int CommitCount { get; private set; }
    public int SavedAtCommit { get; set; }
    public bool Dirty => CommitCount != SavedAtCommit;

    public void Reset()
    {
        undo.Clear();
        redo.Clear();
        pending = null;
        CommitCount = 0;
        SavedAtCommit = 0;
    }

    public void Begin(Func<string> snapshotFactory) => pending ??= snapshotFactory();

    public void CancelPending() => pending = null;

    public bool Commit()
    {
        if (pending == null) return false;
        undo.Push(pending);
        if (undo.Count > maxHistory)
        {
            var arr = undo.ToArray();
            undo.Clear();
            foreach (var s in arr.Take(maxHistory).Reverse()) undo.Push(s);
        }
        redo.Clear();
        pending = null;
        CommitCount++;
        return true;
    }

    public string Undo(Func<string> currentSnapshot)
    {
        if (undo.Count == 0) return null;
        redo.Push(currentSnapshot());
        CommitCount++;
        return undo.Pop();
    }

    public string Redo(Func<string> currentSnapshot)
    {
        if (redo.Count == 0) return null;
        undo.Push(currentSnapshot());
        CommitCount++;
        return redo.Pop();
    }
}