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

    Project _project;
    public Project Project
    {
        get => _project;
        set
        {
            _project = value;
            undo.Clear();
            redo.Clear();
            pending = null;
            CommitCount = 0;
            SavedAtCommit = 0;
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
        set { if (_current == value) return; _current = value; SelNodes.Clear(); SelEdges.Clear(); Changed?.Invoke(); SelectionChanged?.Invoke(); }
    }

    public HashSet<string> SelNodes { get; } = new();
    public HashSet<string> SelEdges { get; } = new();
    public ToolSpec Tool = ToolSpec.Select;

    public event Action Changed;
    public event Action SelectionChanged;
    public event Action ToolChanged;

    readonly Stack<string> undo = new(), redo = new();
    string pending;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int CommitCount { get; private set; }
    public int SavedAtCommit { get; set; }
    public bool Dirty => CommitCount != SavedAtCommit;

    public string Snapshot() => JsonSerializer.Serialize(Project, Json);
    public void Begin() { pending ??= Snapshot(); }
    public void CancelPending() { pending = null; }
    public void Commit(string label)
    {
        if (pending == null) return;
        undo.Push(pending); if (undo.Count > 80) { var arr = undo.ToArray(); undo.Clear(); foreach (var s in arr.Take(80).Reverse()) undo.Push(s); }
        redo.Clear(); pending = null; CommitCount++;
        Changed?.Invoke();
    }
    public void Mutate(Action a, string label) { Begin(); a(); Commit(label); }
    public void Notify() => Changed?.Invoke();
    public void NotifySelection() => SelectionChanged?.Invoke();
    public void SetTool(ToolSpec t) { Tool = t; ToolChanged?.Invoke(); }

    void Restore(string json)
    {
        var p = JsonSerializer.Deserialize<Project>(json, Json);
        var openIds = Open.Select(d => d.Id).ToList(); var curId = Current?.Id;
        _project = p;
        Open.Clear();
        foreach (var id in openIds) { var d = p.FindDiagram(id); if (d != null) Open.Add(d); }
        if (Open.Count == 0) foreach (var d in p.AllDiagrams.Take(1)) Open.Add(d);
        _current = p.FindDiagram(curId) ?? Open.FirstOrDefault();
        var nodeIds = new HashSet<string>(_current?.Nodes.Select(n => n.Id) ?? Enumerable.Empty<string>());
        var edgeIds = new HashSet<string>(_current?.Edges.Select(e => e.Id) ?? Enumerable.Empty<string>());
        SelNodes.IntersectWith(nodeIds); SelEdges.IntersectWith(edgeIds);
        Changed?.Invoke(); SelectionChanged?.Invoke();
    }
    public void Undo() { if (undo.Count == 0) return; redo.Push(Snapshot()); Restore(undo.Pop()); CommitCount++; }
    public void Redo() { if (redo.Count == 0) return; undo.Push(Snapshot()); Restore(redo.Pop()); CommitCount++; }

    // ---------- selection helpers ----------
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
        var def = Catalog.Nodes[kind];
        var n = new UmlNode { Kind = kind, X = x - def.W / 2, Y = y - def.H / 2, W = def.W, H = def.H };
        n.Name = kind switch
        {
            NodeKind.Class => "NewClass", NodeKind.Interface => "NewInterface", NodeKind.Enumeration => "NewEnumeration",
            NodeKind.DataType => "DataType", NodeKind.Object => "obj", NodeKind.Actor => "Actor", NodeKind.UseCase => "Use case",
            NodeKind.Boundary => "System", NodeKind.Lifeline => "obj", NodeKind.Action => "Action", NodeKind.State => "State",
            NodeKind.Component => "Component", NodeKind.DeviceNode => "Device", NodeKind.ExecEnvNode => "Environment",
            NodeKind.Artifact => "Artifact", NodeKind.Package => "Package", NodeKind.Note => "Note",
            NodeKind.ObjectNode => "object", NodeKind.SendSignal => "Signal", NodeKind.AcceptSignal => "Signal",
            NodeKind.InteractionUse => "ref", _ => ""
        };
        if (kind == NodeKind.Lifeline) n.Represents = "Class";
        if (kind == NodeKind.Fragment) { n.Operands.Add(new FragmentOperand { Guard = "condition", Height = 90 }); n.Operands.Add(new FragmentOperand { Guard = "else", Height = 80 }); }
        if (kind == NodeKind.Partition) { n.Lanes.Add("Lane 1"); n.Lanes.Add("Lane 2"); }
        if (kind == NodeKind.Class) n.Attributes.Add(new UmlAttribute { Name = "attribute", Type = "String" });
        if (kind == NodeKind.Fork) n.Orientation = "Vertical";
        Begin();
        Current.Nodes.Add(n);
        if (Catalog.IsContainer(kind)) { var list = Current.Nodes.ToList(); Current.Nodes.Remove(n); Current.Nodes.Insert(0, n); }
        Commit("Add " + def.Label);
        SelectOnly(n);
        return n;
    }

    public (bool ok, string reason) TryConnect(EdgeKind kind, UmlNode s, UmlNode t, double? atY = null)
    {
        if (Current == null) return (false, "No active diagram.");
        var (ok, reason) = Catalog.CanConnect(kind, s, t, Current.Kind);
        if (!ok) return (false, reason);
        if (Current.Kind != DiagramKind.Sequence &&
            Current.Edges.Any(e => e.Kind == kind && e.SourceId == s.Id && e.TargetId == t.Id && kind != EdgeKind.Association))
            return (false, "That relationship already exists.");
        if (kind == EdgeKind.Generalization && CreatesCycle(s, t)) return (false, "Generalization cycle detected.");

        var e = new UmlEdge { Kind = kind, SourceId = s.Id, TargetId = t.Id, Routing = Routing.Straight };
        if (s == t && kind == EdgeKind.Transition) e.LabelDy = -10;

        UmlEdge reply = null;
        if (Current.Kind == DiagramKind.Sequence && e.IsMessage)
        {
            double minY = Math.Max(s.Y + 36, t.Y + 36) + 16;
            double y;
            if (atY.HasValue)
            {
                y = Math.Max(minY, atY.Value);
            }
            else
            {
                var existing = Current.Edges.Where(x => x.IsMessage).ToList();
                y = existing.Count > 0 ? existing.Max(x => x.Y) + 44 : minY + 30;
            }

            if (kind == EdgeKind.MessageCreate)
            {
                t.Y = Math.Max(20, y - 18);
                y = t.Y + 18;
            }

            e.Y = y;
            e.Name = "message()";

            double bottomY = y;
            if (kind == EdgeKind.MessageSync && s != t)
            {
                bottomY = y + 48;
                t.Activations.Add(new Activation { Start = y, End = bottomY, MessageId = e.Id });
                reply = new UmlEdge { Kind = EdgeKind.MessageReply, SourceId = t.Id, TargetId = s.Id, Y = bottomY, Name = "return" };
            }
            else if (kind == EdgeKind.MessageSync && s == t)
            {
                bottomY = y + 36;
                int parentDepth = s.Activations
                    .Where(a => a.Start <= y && a.End >= y)
                    .Select(a => a.Depth + 1)
                    .DefaultIfEmpty(1)
                    .Max();
                s.Activations.Add(new Activation { Start = y, End = bottomY, Depth = parentDepth, MessageId = e.Id });
                reply = new UmlEdge { Kind = EdgeKind.MessageReply, SourceId = s.Id, TargetId = s.Id, Y = bottomY, Name = "return", IsSelfReply = true };
            }
            else if (kind == EdgeKind.MessageReply)
            {
                var act = s.Activations.Where(a => a.Start <= y).OrderByDescending(a => a.Start).FirstOrDefault();
                if (act != null) act.End = y;
            }
            else if (kind == EdgeKind.MessageDelete)
            {
                foreach (var a in t.Activations) a.End = Math.Min(a.End, y);
            }

            s.H = Math.Max(s.H, bottomY - s.Y + 50);
            t.H = Math.Max(t.H, bottomY - t.Y + 50);
        }

        Mutate(() =>
        {
            Current.Edges.Add(e);
            if (reply != null) Current.Edges.Add(reply);
        }, "Connect " + Catalog.Edges[kind].Label);
        SelectOnly(e);
        return (true, "");
    }

    bool CreatesCycle(UmlNode child, UmlNode parent)
    {
        var seen = new HashSet<string>(); var stack = new Stack<UmlNode>(); stack.Push(parent);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.Id == child.Id) return true;
            if (!seen.Add(n.Id)) continue;
            foreach (var e in Current.Edges.Where(x => x.Kind == EdgeKind.Generalization && x.SourceId == n.Id))
            { var p = Current.Node(e.TargetId); if (p != null) stack.Push(p); }
        }
        return false;
    }

    public void DeleteSelection()
    {
        if (SelNodes.Count == 0 && SelEdges.Count == 0) return;
        Mutate(() =>
        {
            var nodes = Current.Nodes.Where(n => SelNodes.Contains(n.Id)).ToList();
            foreach (var n in nodes)
            {
                Current.Nodes.Remove(n);
                foreach (var e in Current.Edges.Where(e => e.SourceId == n.Id || e.TargetId == n.Id).ToList()) Current.Edges.Remove(e);
                foreach (var attached in Current.Nodes.Where(x => x.AttachParentId == n.Id).ToList()) Current.Nodes.Remove(attached);
            }
            foreach (var id in SelEdges.ToList()) { var e = Current.Edge(id); if (e != null) Current.Edges.Remove(e); }
            SelNodes.Clear(); SelEdges.Clear();
        }, "Delete");
        NotifySelection();
    }

    public void DuplicateSelection()
    {
        if (SelNodes.Count == 0) return;
        Mutate(() =>
        {
            var map = new Dictionary<string, string>();
            var originals = Current.Nodes.Where(n => SelNodes.Contains(n.Id)).ToList();
            var newSel = new List<string>();
            foreach (var n in originals)
            {
                var json = JsonSerializer.Serialize(n, Json);
                var copy = JsonSerializer.Deserialize<UmlNode>(json, Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10];
                copy.X += 28; copy.Y += 28; copy.Name = n.Name.Length > 0 ? n.Name + "Copy" : n.Name;
                copy.AttachParentId = null;
                Current.Nodes.Add(copy); map[n.Id] = copy.Id; newSel.Add(copy.Id);
            }
            foreach (var e in Current.Edges.Where(e => map.ContainsKey(e.SourceId) && map.ContainsKey(e.TargetId)).ToList())
            {
                var copy = JsonSerializer.Deserialize<UmlEdge>(JsonSerializer.Serialize(e, Json), Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10]; copy.SourceId = map[e.SourceId]; copy.TargetId = map[e.TargetId];
                Current.Edges.Add(copy);
            }
            SelNodes.Clear(); foreach (var id in newSel) SelNodes.Add(id);
        }, "Duplicate");
        NotifySelection();
    }

    public void Align(string how)
    {
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