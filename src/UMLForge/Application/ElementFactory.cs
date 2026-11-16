namespace UmlForge;

public static class ElementFactory
{
    public static UmlNode CreateNode(NodeKind kind, double x, double y)
    {
        var def = Catalog.Nodes[kind];
        var n = new UmlNode
        {
            Kind = kind,
            X = x - def.W / 2,
            Y = y - def.H / 2,
            W = def.W,
            H = def.H,
            Name = DefaultName(kind)
        };

        if (kind == NodeKind.Lifeline) n.Represents = "Class";
        if (kind == NodeKind.Fragment)
        {
            n.Operands.Add(new FragmentOperand { Guard = "condition", Height = 90 });
            n.Operands.Add(new FragmentOperand { Guard = "else", Height = 80 });
        }
        if (kind == NodeKind.Partition)
        {
            n.Lanes.Add("Lane 1");
            n.Lanes.Add("Lane 2");
        }
        if (kind == NodeKind.Class)
            n.Attributes.Add(new UmlAttribute { Name = "attribute", Type = "String" });
        if (kind == NodeKind.Fork)
            n.Orientation = "Vertical";

        return n;
    }

    static string DefaultName(NodeKind kind) => kind switch
    {
        NodeKind.Class => "NewClass",
        NodeKind.Interface => "NewInterface",
        NodeKind.Enumeration => "NewEnumeration",
        NodeKind.DataType => "DataType",
        NodeKind.Object => "obj",
        NodeKind.Actor => "Actor",
        NodeKind.UseCase => "Use case",
        NodeKind.Boundary => "System",
        NodeKind.Lifeline => "obj",
        NodeKind.Action => "Action",
        NodeKind.State => "State",
        NodeKind.Component => "Component",
        NodeKind.DeviceNode => "Device",
        NodeKind.ExecEnvNode => "Environment",
        NodeKind.Artifact => "Artifact",
        NodeKind.Package => "Package",
        NodeKind.Note => "Note",
        NodeKind.ObjectNode => "object",
        NodeKind.SendSignal => "Signal",
        NodeKind.AcceptSignal => "Signal",
        NodeKind.InteractionUse => "ref",
        _ => ""
    };

    public static (bool ok, string reason, UmlEdge edge, UmlEdge reply) CreateConnection(
        Diagram diagram, EdgeKind kind, UmlNode s, UmlNode t, double? atY = null)
    {
        if (diagram == null) return (false, "No active diagram.", null, null);
        var (ok, reason) = Catalog.CanConnect(kind, s, t, diagram.Kind);
        if (!ok) return (false, reason, null, null);

        if (diagram.Kind != DiagramKind.Sequence &&
            diagram.Edges.Any(e => e.Kind == kind && e.SourceId == s.Id && e.TargetId == t.Id && kind != EdgeKind.Association))
            return (false, "That relationship already exists.", null, null);

        if (kind == EdgeKind.Generalization && CreatesCycle(diagram, s, t))
            return (false, "Generalization cycle detected.", null, null);

        var e = new UmlEdge { Kind = kind, SourceId = s.Id, TargetId = t.Id, Routing = Routing.Straight };
        if (s == t && kind == EdgeKind.Transition) e.LabelDy = -10;

        UmlEdge reply = null;
        if (diagram.Kind == DiagramKind.Sequence && e.IsMessage)
            reply = ConfigureSequenceMessage(diagram, e, s, t, atY);

        return (true, "", e, reply);
    }

    static UmlEdge ConfigureSequenceMessage(Diagram diagram, UmlEdge e, UmlNode s, UmlNode t, double? atY)
    {
        double minY = Math.Max(s.Y + 36, t.Y + 36) + 16;
        double y;
        if (atY.HasValue)
        {
            y = Math.Max(minY, atY.Value);
        }
        else
        {
            var existing = diagram.Edges.Where(x => x.IsMessage).ToList();
            y = existing.Count > 0 ? existing.Max(x => x.Y) + 44 : minY + 30;
        }

        if (e.Kind == EdgeKind.MessageCreate)
        {
            t.Y = Math.Max(20, y - 18);
            y = t.Y + 18;
        }

        e.Y = y;
        e.Name = "message()";

        UmlEdge reply = null;
        double bottomY = y;

        if (e.Kind == EdgeKind.MessageSync && s != t)
        {
            bottomY = y + 48;
            t.Activations.Add(new Activation { Start = y, End = bottomY, MessageId = e.Id });
            reply = new UmlEdge { Kind = EdgeKind.MessageReply, SourceId = t.Id, TargetId = s.Id, Y = bottomY, Name = "return" };
        }
        else if (e.Kind == EdgeKind.MessageSync && s == t)
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
        else if (e.Kind == EdgeKind.MessageReply)
        {
            var act = s.Activations.Where(a => a.Start <= y).OrderByDescending(a => a.Start).FirstOrDefault();
            if (act != null) act.End = y;
        }
        else if (e.Kind == EdgeKind.MessageDelete)
        {
            foreach (var a in t.Activations) a.End = Math.Min(a.End, y);
        }

        s.H = Math.Max(s.H, bottomY - s.Y + 50);
        t.H = Math.Max(t.H, bottomY - t.Y + 50);
        return reply;
    }

    static bool CreatesCycle(Diagram diagram, UmlNode child, UmlNode parent)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<UmlNode>();
        stack.Push(parent);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.Id == child.Id) return true;
            if (!seen.Add(n.Id)) continue;
            foreach (var e in diagram.Edges.Where(x => x.Kind == EdgeKind.Generalization && x.SourceId == n.Id))
            {
                var p = diagram.Node(e.TargetId);
                if (p != null) stack.Push(p);
            }
        }
        return false;
    }
}