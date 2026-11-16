using System.Windows;

namespace UmlForge;

public static class ValidationService
{
    public static List<Issue> Validate(Diagram d)
    {
        var issues = new List<Issue>();
        void Add(Severity sev, string msg, string id) => issues.Add(new Issue { Severity = sev, Message = msg, ElementId = id });

        var named = d.Nodes.Where(n => n.Kind is not (NodeKind.Initial or NodeKind.StateInitial or NodeKind.ActivityFinal or NodeKind.StateFinal
            or NodeKind.FlowFinal or NodeKind.Junction or NodeKind.Terminate or NodeKind.Choice or NodeKind.Decision or NodeKind.Fork
            or NodeKind.HistoryShallow or NodeKind.HistoryDeep or NodeKind.Port or NodeKind.Fragment)).ToList();
        foreach (var n in named)
            if (string.IsNullOrWhiteSpace(n.Name)) Add(Severity.Warning, $"{Catalog.Nodes[n.Kind].Label} has no name.", n.Id);
        foreach (var g in named.Where(n => n.IsClassifier || n.Kind == NodeKind.Component).GroupBy(n => n.Name).Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key)))
            Add(Severity.Warning, $"Duplicate classifier name “{g.Key}” in this diagram.", g.First().Id);

        foreach (var e in d.Edges)
        {
            var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
            if (s == null || t == null) { Add(Severity.Error, "Dangling relationship (missing endpoint).", e.Id); continue; }
            var (ok, reason) = Catalog.CanConnect(e.Kind, s, t, d.Kind);
            if (!ok) Add(Severity.Error, reason, e.Id);
        }

        if (d.Kind == DiagramKind.Class)
        {
            foreach (var n in d.Nodes.Where(n => n.Kind == NodeKind.Class && !n.IsAbstract))
                foreach (var o in n.Operations.Where(o => o.IsAbstract))
                    Add(Severity.Error, $"Abstract operation “{o.Name}” in concrete class “{n.Name}”.", n.Id);
            foreach (var g in d.Nodes.Where(n => n.IsClassifier).GroupBy(n => n.Id))
            {
                var id = g.Key;
                var owners = d.Edges.Where(e => e.Kind == EdgeKind.Composition && e.TargetId == id).ToList();
                if (owners.Count > 1) Add(Severity.Error, $"“{d.Node(id).Name}” is part of {owners.Count} composites — composition ownership must be exclusive.", id);
            }
            foreach (var e in d.Edges.Where(e => e.Kind == EdgeKind.Composition))
            {
                var m = e.SourceMultiplicity;
                if (!string.IsNullOrWhiteSpace(m) && (m.Contains("*") || m.EndsWith("2") || m.EndsWith("3")))
                    Add(Severity.Warning, "Whole-end multiplicity of a composition should be 0..1 or 1.", e.Id);
            }
        }
        if (d.Kind == DiagramKind.StateMachine)
        {
            foreach (var e in d.Edges.Where(e => e.Kind == EdgeKind.Transition))
            {
                var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
                if (s?.Kind == NodeKind.StateInitial && (!string.IsNullOrWhiteSpace(e.Trigger) || !string.IsNullOrWhiteSpace(e.Guard)))
                    Add(Severity.Error, "A transition from the initial pseudostate must not have a trigger or guard.", e.Id);
                if (s?.Kind == NodeKind.StateFinal) Add(Severity.Error, "A final state cannot have outgoing transitions.", e.Id);
                if (t?.Kind == NodeKind.StateInitial) Add(Severity.Error, "The initial pseudostate cannot have incoming transitions.", e.Id);
            }
            var initial = d.Nodes.FirstOrDefault(n => n.Kind == NodeKind.StateInitial);
            if (initial != null)
            {
                var reach = new HashSet<string>(); var stack = new Stack<string>(); stack.Push(initial.Id);
                while (stack.Count > 0) { var id = stack.Pop(); if (!reach.Add(id)) continue; foreach (var e in d.Edges.Where(x => x.SourceId == id)) stack.Push(e.TargetId); }
                foreach (var n in d.Nodes.Where(n => n.Kind == NodeKind.State && !reach.Contains(n.Id)))
                    Add(Severity.Warning, $"State “{n.Name}” is unreachable from the initial pseudostate.", n.Id);
            }
        }
        if (d.Kind == DiagramKind.Activity)
        {
            foreach (var e in d.Edges)
            {
                var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
                if (s?.Kind == NodeKind.Initial && d.Edges.Any(x => x.TargetId == s.Id)) Add(Severity.Error, "Initial node cannot have incoming flows.", s.Id);
                if (s?.Kind is NodeKind.ActivityFinal or NodeKind.FlowFinal) Add(Severity.Error, "Final nodes cannot have outgoing flows.", e.Id);
                if (t?.Kind == NodeKind.Initial) Add(Severity.Error, "Initial node cannot have incoming flows.", e.Id);
            }
            foreach (var dec in d.Nodes.Where(n => n.Kind == NodeKind.Decision))
            {
                var outs = d.Edges.Where(e => e.SourceId == dec.Id).ToList();
                if (outs.Count >= 2 && outs.Any(e => string.IsNullOrWhiteSpace(e.Guard)))
                    Add(Severity.Warning, "Decision outgoing flows should each carry a [guard].", dec.Id);
            }
            foreach (var n in d.Nodes.Where(n => n.Kind == NodeKind.Action))
                if (!d.Edges.Any(e => e.TargetId == n.Id) || !d.Edges.Any(e => e.SourceId == n.Id))
                    Add(Severity.Warning, $"Action “{n.Name}” is dangling (missing incoming or outgoing flow).", n.Id);
        }
        if (d.Kind == DiagramKind.Sequence)
        {
            foreach (var ll in d.Nodes.Where(n => n.Kind == NodeKind.Lifeline))
            {
                var create = d.Edges.FirstOrDefault(e => e.Kind == EdgeKind.MessageCreate && e.TargetId == ll.Id);
                if (create != null)
                {
                    double cy = ll.Y + 18;
                    if (d.Edges.Any(e => e.IsMessage && e.TargetId == ll.Id && e.Kind != EdgeKind.MessageCreate && e.Y < cy))
                        Add(Severity.Warning, $"Lifeline “{ll.Name}” receives messages before it is created.", ll.Id);
                }
                var delete = d.Edges.FirstOrDefault(e => e.Kind == EdgeKind.MessageDelete && e.TargetId == ll.Id);
                if (delete != null && d.Edges.Any(e => e.IsMessage && e.TargetId == ll.Id && e.Kind != EdgeKind.MessageDelete && e.Y > delete.Y))
                    Add(Severity.Error, $"Lifeline “{ll.Name}” receives messages after deletion.", ll.Id);
            }
        }
        if (d.Kind == DiagramKind.UseCase)
        {
            foreach (var e in d.Edges.Where(e => e.Kind is EdgeKind.Include or EdgeKind.Extend))
            {
                var chain = d.Edges.Where(x => x.Kind == e.Kind).ToList();
                var seen = new HashSet<string>(); var stack = new Stack<string>(); stack.Push(e.TargetId);
                while (stack.Count > 0) { var id = stack.Pop(); if (id == e.SourceId) { Add(Severity.Warning, $"«{e.Kind.ToString().ToLower()}» cycle detected.", e.Id); break; } if (!seen.Add(id)) continue; foreach (var x in chain.Where(x => x.SourceId == id)) stack.Push(x.TargetId); }
            }
        }
        if (d.Kind == DiagramKind.Component)
            foreach (var req in d.Nodes.Where(n => n.Kind == NodeKind.RequiredInterface))
                if (!d.Edges.Any(e => e.SourceId == req.Id || e.TargetId == req.Id))
                    Add(Severity.Warning, "Required interface is not connected to a provided interface.", req.Id);
        if (d.Kind == DiagramKind.Deployment)
            foreach (var a in d.Nodes.Where(n => n.Kind == NodeKind.Artifact))
            {
                bool deployed = d.Edges.Any(e => e.Kind == EdgeKind.Deployment && e.SourceId == a.Id);
                bool inside = d.Nodes.Any(n => n.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode && Geo.Bounds(n, 13).Contains(new Point(a.X + a.W / 2, a.Y + a.H / 2)));
                if (!deployed && !inside) Add(Severity.Warning, $"Artifact “{a.Name}” is not deployed to any node.", a.Id);
            }
        return issues;
    }
}