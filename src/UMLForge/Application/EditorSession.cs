using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmlForge;

public class Editor
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    readonly UndoRedoManager history = new();

    Project _project;
    public Project Project
    {
        get => _project;
        set
        {
            _project = value;
            history.Reset();
            Open.Clear();
            foreach (var d in value.AllDiagrams.Take(1)) Open.Add(d);
            Current = Open.FirstOrDefault();
            Changed?.Invoke();
        }
    }

    public ObservableCollection<Diagram> Open { get; } = new();

    Diagram _current;
    public Diagram Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            SelNodes.Clear();
            SelEdges.Clear();
            Changed?.Invoke();
            SelectionChanged?.Invoke();
        }
    }

    public HashSet<string> SelNodes { get; } = new();
    public HashSet<string> SelEdges { get; } = new();
    public ToolSpec Tool = ToolSpec.Select;

    public event Action Changed;
    public event Action SelectionChanged;
    public event Action ToolChanged;

    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;
    public int CommitCount => history.CommitCount;
    public int SavedAtCommit { get => history.SavedAtCommit; set => history.SavedAtCommit = value; }
    public bool Dirty => history.Dirty;

    public string Snapshot() => JsonSerializer.Serialize(Project, Json);
    public void Begin() => history.Begin(Snapshot);
    public void CancelPending() => history.CancelPending();
    public void Commit(string label) { if (history.Commit()) Changed?.Invoke(); }
    public void Mutate(Action a, string label) { Begin(); a(); Commit(label); }
    public void Notify() => Changed?.Invoke();
    public void NotifySelection() => SelectionChanged?.Invoke();
    public void SetTool(ToolSpec t) { Tool = t; ToolChanged?.Invoke(); }

    void Restore(string json)
    {
        var p = JsonSerializer.Deserialize<Project>(json, Json);
        var openIds = Open.Select(d => d.Id).ToList();
        var curId = Current?.Id;
        _project = p;
        Open.Clear();
        foreach (var id in openIds) { var d = p.FindDiagram(id); if (d != null) Open.Add(d); }
        if (Open.Count == 0) foreach (var d in p.AllDiagrams.Take(1)) Open.Add(d);
        _current = p.FindDiagram(curId) ?? Open.FirstOrDefault();

        var nodeIds = new HashSet<string>(_current?.Nodes.Select(n => n.Id) ?? Enumerable.Empty<string>());
        var edgeIds = new HashSet<string>(_current?.Edges.Select(e => e.Id) ?? Enumerable.Empty<string>());
        SelNodes.IntersectWith(nodeIds);
        SelEdges.IntersectWith(edgeIds);
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void Undo() { var snap = history.Undo(Snapshot); if (snap != null) Restore(snap); }
    public void Redo() { var snap = history.Redo(Snapshot); if (snap != null) Restore(snap); }

    // ---------- selection & tabs ----------
    public void SelectOnly(UmlNode n) { SelNodes.Clear(); SelEdges.Clear(); if (n != null) SelNodes.Add(n.Id); NotifySelection(); }
    public void SelectOnly(UmlEdge e) { SelNodes.Clear(); SelEdges.Clear(); if (e != null) SelEdges.Add(e.Id); NotifySelection(); }
    public void ClearSelection() { SelNodes.Clear(); SelEdges.Clear(); NotifySelection(); }

    public void OpenDiagram(Diagram d)
    {
        if (!Open.Contains(d)) Open.Add(d);
        Current = d;
    }

    // ---------- element operations ----------
    public UmlNode AddNode(NodeKind kind, double x, double y)
    {
        if (Current == null) return null;
        var n = ElementFactory.CreateNode(kind, x, y);
        Begin();
        Current.Nodes.Add(n);
        if (Catalog.IsContainer(kind))
        {
            Current.Nodes.Remove(n);
            Current.Nodes.Insert(0, n);
        }
        Commit("Add " + Catalog.Nodes[kind].Label);
        SelectOnly(n);
        return n;
    }

    public (bool ok, string reason) TryConnect(EdgeKind kind, UmlNode s, UmlNode t, double? atY = null)
    {
        var (ok, reason, edge, reply) = ElementFactory.CreateConnection(Current, kind, s, t, atY);
        if (!ok) return (false, reason);

        Mutate(() =>
        {
            Current.Edges.Add(edge);
            if (reply != null) Current.Edges.Add(reply);
        }, "Connect " + Catalog.Edges[kind].Label);
        SelectOnly(edge);
        return (true, "");
    }

    public void DeleteSelection()
    {
        if (Current == null || (SelNodes.Count == 0 && SelEdges.Count == 0)) return;
        Mutate(() =>
        {
            var nodes = Current.Nodes.Where(n => SelNodes.Contains(n.Id)).ToList();
            foreach (var n in nodes)
            {
                Current.Nodes.Remove(n);
                foreach (var e in Current.Edges.Where(e => e.SourceId == n.Id || e.TargetId == n.Id).ToList())
                    Current.Edges.Remove(e);
                foreach (var attached in Current.Nodes.Where(x => x.AttachParentId == n.Id).ToList())
                    Current.Nodes.Remove(attached);
            }
            foreach (var id in SelEdges.ToList())
            {
                var e = Current.Edge(id);
                if (e != null)
                {
                    Current.Edges.Remove(e);
                    foreach (var n in Current.Nodes)
                        for (int i = n.Activations.Count - 1; i >= 0; i--)
                            if (n.Activations[i].MessageId == e.Id)
                                n.Activations.RemoveAt(i);
                }
            }
            SelNodes.Clear();
            SelEdges.Clear();
        }, "Delete");
        NotifySelection();
    }

    public void DuplicateSelection()
    {
        if (Current == null || SelNodes.Count == 0) return;
        Mutate(() =>
        {
            var map = new Dictionary<string, string>();
            var originals = Current.Nodes.Where(n => SelNodes.Contains(n.Id)).ToList();
            var newSel = new List<string>();
            foreach (var n in originals)
            {
                var copy = JsonSerializer.Deserialize<UmlNode>(JsonSerializer.Serialize(n, Json), Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10];
                copy.X += 28;
                copy.Y += 28;
                copy.Name = n.Name.Length > 0 ? n.Name + "Copy" : n.Name;
                copy.AttachParentId = null;
                Current.Nodes.Add(copy);
                map[n.Id] = copy.Id;
                newSel.Add(copy.Id);
            }
            foreach (var e in Current.Edges.Where(e => map.ContainsKey(e.SourceId) && map.ContainsKey(e.TargetId)).ToList())
            {
                var copy = JsonSerializer.Deserialize<UmlEdge>(JsonSerializer.Serialize(e, Json), Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10];
                copy.SourceId = map[e.SourceId];
                copy.TargetId = map[e.TargetId];
                Current.Edges.Add(copy);
            }
            SelNodes.Clear();
            foreach (var id in newSel) SelNodes.Add(id);
        }, "Duplicate");
        NotifySelection();
    }

    public void Align(string how)
    {
        if (Current == null) return;
        var nodes = Current.Nodes.Where(n => SelNodes.Contains(n.Id)).ToList();
        if (nodes.Count < 2) return;
        Mutate(() =>
        {
            var bs = nodes.Select(n => Geo.Bounds(n, 13)).ToList();
            switch (how)
            {
                case "left": { double v = bs.Min(b => b.X); foreach (var n in nodes) n.X = v; break; }
                case "center": { double v = bs.Average(b => b.X + b.Width / 2); foreach (var n in nodes) n.X = v - Geo.Bounds(n, 13).Width / 2; break; }
                case "right": { double v = bs.Max(b => b.Right); foreach (var n in nodes) n.X = v - Geo.Bounds(n, 13).Width; break; }
                case "top": { double v = bs.Min(b => b.Y); foreach (var n in nodes) n.Y = v; break; }
                case "middle": { double v = bs.Average(b => b.Y + b.Height / 2); foreach (var n in nodes) n.Y = v - Geo.Bounds(n, 13).Height / 2; break; }
                case "bottom": { double v = bs.Max(b => b.Bottom); foreach (var n in nodes) n.Y = v - Geo.Bounds(n, 13).Height; break; }
            }
        }, "Align");
    }
}