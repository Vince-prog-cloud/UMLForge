using System.IO;
using System.Text;

namespace UmlForge;

public static class XmiExporter
{
    static string X(string s) => System.Security.SecurityElement.Escape(s ?? "") ?? "";
    public static void Export(Project project, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<xmi:XMI xmi:version=\"2.5.1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns:uml=\"http://www.eclipse.org/uml2/5.0.0/UML\">");
        sb.AppendLine($"  <uml:Model xmi:id=\"_model\" name=\"{X(project.Name)}\">");
        var ids = new Dictionary<string, string>();
        var primitives = new HashSet<string>();
        string IdOf(UmlNode n)
        {
            if (ids.TryGetValue(n.Id, out var id)) return id;
            id = "_" + n.Id;
            ids[n.Id] = id;
            return id;
        }
        string TypeRef(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            var classifier = project.AllDiagrams.SelectMany(d => d.Nodes).FirstOrDefault(n => n.IsClassifier && n.Name == type);
            if (classifier != null) return IdOf(classifier);
            primitives.Add(type); return "_prim_" + type;
        }
        void Mult(StringBuilder s, string mult)
        {
            if (string.IsNullOrWhiteSpace(mult)) return;
            string lo = "0", hi = "*";
            if (mult.Contains("..")) { var p = mult.Split(".."); lo = p[0].Trim(); hi = p[1].Trim(); }
            else { lo = hi = mult.Trim(); }
            if (lo != "1") s.AppendLine($"        <lowerValue xmi:type=\"uml:LiteralInteger\" xmi:id=\"_{Guid.NewGuid():N}\" value=\"{X(lo)}\"/>");
            if (hi != "1") s.AppendLine($"        <upperValue xmi:type=\"uml:LiteralUnlimitedNatural\" xmi:id=\"_{Guid.NewGuid():N}\" value=\"{X(hi)}\"/>");
        }

        foreach (var pkg in project.Packages)
        {
            sb.AppendLine($"    <packagedElement xmi:type=\"uml:Package\" xmi:id=\"{pkg.Id}\" name=\"{X(pkg.Name)}\">");
            var nodes = pkg.Diagrams.SelectMany(d => d.Nodes).Where(n => n.Kind is not NodeKind.Note).ToList();
            var emitted = new HashSet<string>();
            foreach (var n in nodes)
            {
                string id = IdOf(n); if (!emitted.Add(id)) continue;
                switch (n.Kind)
                {
                    case NodeKind.Class or NodeKind.Interface or NodeKind.DataType or NodeKind.Enumeration or NodeKind.Component or NodeKind.Actor or NodeKind.UseCase or NodeKind.Package or NodeKind.DeviceNode or NodeKind.ExecEnvNode or NodeKind.Artifact:
                        string type = n.Kind switch
                        {
                            NodeKind.Class => "uml:Class",
                            NodeKind.Interface => "uml:Interface",
                            NodeKind.DataType => "uml:DataType",
                            NodeKind.Enumeration => "uml:Enumeration",
                            NodeKind.Component => "uml:Component",
                            NodeKind.Actor => "uml:Actor",
                            NodeKind.UseCase => "uml:UseCase",
                            NodeKind.Package => "uml:Package",
                            NodeKind.DeviceNode => "uml:Device",
                            NodeKind.ExecEnvNode => "uml:ExecutionEnvironment",
                            _ => "uml:Artifact"
                        };
                        sb.AppendLine($"      <packagedElement xmi:type=\"{type}\" xmi:id=\"{id}\" name=\"{X(n.Name)}\"{(n.IsAbstract ? " isAbstract=\"true\"" : "")}>");
                        foreach (var a in n.Attributes)
                        {
                            string tr = TypeRef(a.Type);
                            sb.AppendLine($"        <ownedAttribute xmi:type=\"uml:Property\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(a.Name)}\" visibility=\"{a.Visibility.ToString().ToLower()}\"{(a.IsStatic ? " isStatic=\"true\"" : "")}{(a.IsReadOnly ? " isReadOnly=\"true\"" : "")}{(tr != null ? $" type=\"{tr}\"" : "")}>");
                            Mult(sb, a.Multiplicity);
                            if (!string.IsNullOrWhiteSpace(a.Default)) sb.AppendLine($"          <defaultValue xmi:type=\"uml:LiteralString\" xmi:id=\"_{Guid.NewGuid():N}\" value=\"{X(a.Default)}\"/>");
                            sb.AppendLine("        </ownedAttribute>");
                        }
                        foreach (var lit in n.Literals)
                            sb.AppendLine($"        <ownedLiteral xmi:type=\"uml:EnumerationLiteral\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(lit)}\"/>");
                        foreach (var o in n.Operations)
                        {
                            string tr = TypeRef(o.ReturnType);
                            sb.AppendLine($"        <ownedOperation xmi:type=\"uml:Operation\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(o.Name)}\"{(o.IsAbstract ? " isAbstract=\"true\"" : "")}{(o.IsStatic ? " isStatic=\"true\"" : "")}>");
                            if (tr != null) sb.AppendLine($"          <ownedParameter xmi:type=\"uml:Parameter\" xmi:id=\"_{Guid.NewGuid():N}\" direction=\"return\" type=\"{tr}\"/>");
                            foreach (var p in o.Parameters.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var parts = p.Split(':');
                                sb.AppendLine($"          <ownedParameter xmi:type=\"uml:Parameter\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(parts[0].Trim())}\"{(parts.Length > 1 ? $" type=\"{TypeRef(parts[1].Trim())}\"" : "")}/>");
                            }
                            sb.AppendLine("        </ownedOperation>");
                        }
                        foreach (var d in pkg.Diagrams)
                        {
                            foreach (var g in d.Edges.Where(e => e.Kind == EdgeKind.Generalization && e.SourceId == n.Id))
                            { var t = d.Node(g.TargetId); if (t != null) sb.AppendLine($"        <generalization xmi:type=\"uml:Generalization\" xmi:id=\"_{Guid.NewGuid():N}\" general=\"{IdOf(t)}\"/>"); }
                            foreach (var r in d.Edges.Where(e => e.Kind == EdgeKind.Realization && e.SourceId == n.Id))
                            { var t = d.Node(r.TargetId); if (t != null) sb.AppendLine($"        <interfaceRealization xmi:type=\"uml:InterfaceRealization\" xmi:id=\"_{Guid.NewGuid():N}\" contract=\"{IdOf(t)}\" implementingClassifier=\"{id}\"/>"); }
                            foreach (var inc in d.Edges.Where(e => e.Kind == EdgeKind.Include && e.SourceId == n.Id))
                            { var t = d.Node(inc.TargetId); if (t != null) sb.AppendLine($"        <include xmi:type=\"uml:Include\" xmi:id=\"_{Guid.NewGuid():N}\" addition=\"{IdOf(t)}\"/>"); }
                            foreach (var ext in d.Edges.Where(e => e.Kind == EdgeKind.Extend && e.SourceId == n.Id))
                            { var t = d.Node(ext.TargetId); if (t != null) sb.AppendLine($"        <extend xmi:type=\"uml:Extend\" xmi:id=\"_{Guid.NewGuid():N}\" extendedCase=\"{IdOf(t)}\"/>"); }
                        }
                        sb.AppendLine("      </packagedElement>");
                        break;
                }
            }
            var assocDone = new HashSet<string>();
            foreach (var d in pkg.Diagrams)
                foreach (var e in d.Edges.Where(e => e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition or EdgeKind.Link or EdgeKind.CommunicationPath))
                {
                    var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
                    if (s == null || t == null) continue;
                    string key = e.Kind + "|" + IdOf(s) + "|" + IdOf(t) + "|" + e.Name;
                    if (!assocDone.Add(key)) continue;
                    string aid = "_" + Guid.NewGuid().ToString("N")[..12];
                    string e1 = "_" + Guid.NewGuid().ToString("N")[..12], e2 = "_" + Guid.NewGuid().ToString("N")[..12];
                    sb.AppendLine($"      <packagedElement xmi:type=\"uml:Association\" xmi:id=\"{aid}\" name=\"{X(e.Name)}\" memberEnd=\"{e1} {e2}\">");
                    string agg = e.Kind == EdgeKind.Aggregation ? " aggregation=\"shared\"" : e.Kind == EdgeKind.Composition ? " aggregation=\"composite\"" : "";
                    sb.AppendLine($"        <ownedEnd xmi:type=\"uml:Property\" xmi:id=\"{e1}\" name=\"{X(e.SourceRole)}\" type=\"{IdOf(s)}\" association=\"{aid}\"{agg}>");
                    Mult(sb, e.SourceMultiplicity); sb.AppendLine("        </ownedEnd>");
                    sb.AppendLine($"        <ownedEnd xmi:type=\"uml:Property\" xmi:id=\"{e2}\" name=\"{X(e.TargetRole)}\" type=\"{IdOf(t)}\" association=\"{aid}\">");
                    Mult(sb, e.TargetMultiplicity); sb.AppendLine("        </ownedEnd>");
                    sb.AppendLine("      </packagedElement>");
                }
            foreach (var d in pkg.Diagrams)
                foreach (var e in d.Edges.Where(e => e.Kind is EdgeKind.Dependency or EdgeKind.Usage or EdgeKind.Deployment or EdgeKind.Manifest or EdgeKind.PackageImport or EdgeKind.PackageMerge))
                {
                    var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
                    if (s == null || t == null) continue;
                    sb.AppendLine($"      <packagedElement xmi:type=\"uml:Dependency\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(e.Name)}\" client=\"{IdOf(s)}\" supplier=\"{IdOf(t)}\"/>");
                }
            foreach (var d in pkg.Diagrams.Where(d => d.Kind == DiagramKind.StateMachine))
            {
                string smId = "_" + Guid.NewGuid().ToString("N")[..12];
                sb.AppendLine($"      <packagedElement xmi:type=\"uml:StateMachine\" xmi:id=\"{smId}\" name=\"{X(d.Name)}\">");
                sb.AppendLine($"        <region xmi:type=\"uml:Region\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"Region\">");
                foreach (var n in d.Nodes.Where(n => n.IsStateish))
                {
                    string vt = n.Kind switch
                    {
                        NodeKind.State => "uml:State",
                        NodeKind.StateInitial => "uml:Pseudostate",
                        NodeKind.StateFinal => "uml:FinalState",
                        NodeKind.Choice or NodeKind.Junction or NodeKind.Terminate or NodeKind.HistoryShallow or NodeKind.HistoryDeep => "uml:Pseudostate",
                        _ => "uml:State"
                    };
                    string kindAttr = n.Kind switch
                    {
                        NodeKind.StateInitial => " kind=\"initial\"",
                        NodeKind.Choice => " kind=\"choice\"",
                        NodeKind.Junction => " kind=\"junction\"",
                        NodeKind.Terminate => " kind=\"terminate\"",
                        NodeKind.HistoryShallow => " kind=\"shallowHistory\"",
                        NodeKind.HistoryDeep => " kind=\"deepHistory\"",
                        _ => ""
                    };
                    sb.AppendLine($"          <subvertex xmi:type=\"{vt}\" xmi:id=\"{n.Id}\" name=\"{X(n.Name)}\"{kindAttr}>");
                    if (n.Kind == NodeKind.State)
                    {
                        if (!string.IsNullOrWhiteSpace(n.Entry)) sb.AppendLine($"            <entry xmi:type=\"uml:OpaqueBehavior\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(n.Entry)}</body></entry>");
                        if (!string.IsNullOrWhiteSpace(n.Exit)) sb.AppendLine($"            <exit xmi:type=\"uml:OpaqueBehavior\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(n.Exit)}</body></exit>");
                        if (!string.IsNullOrWhiteSpace(n.Do)) sb.AppendLine($"            <doActivity xmi:type=\"uml:OpaqueBehavior\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(n.Do)}</body></doActivity>");
                    }
                    sb.AppendLine("          </subvertex>");
                }
                foreach (var e in d.Edges.Where(e => e.Kind == EdgeKind.Transition))
                {
                    sb.AppendLine($"          <transition xmi:type=\"uml:Transition\" xmi:id=\"{e.Id}\" source=\"{e.SourceId}\" target=\"{e.TargetId}\">");
                    if (!string.IsNullOrWhiteSpace(e.Trigger)) sb.AppendLine($"            <trigger xmi:type=\"uml:Trigger\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(e.Trigger)}\"/>");
                    if (!string.IsNullOrWhiteSpace(e.Guard)) sb.AppendLine($"            <guard xmi:type=\"uml:OpaqueExpression\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(e.Guard)}</body></guard>");
                    if (!string.IsNullOrWhiteSpace(e.Effect)) sb.AppendLine($"            <effect xmi:type=\"uml:OpaqueBehavior\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(e.Effect)}</body></effect>");
                    sb.AppendLine("          </transition>");
                }
                sb.AppendLine("        </region></packagedElement>");
            }
            foreach (var d in pkg.Diagrams.Where(d => d.Kind == DiagramKind.Activity))
            {
                string aid = "_" + Guid.NewGuid().ToString("N")[..12];
                sb.AppendLine($"      <packagedElement xmi:type=\"uml:Activity\" xmi:id=\"{aid}\" name=\"{X(d.Name)}\">");
                foreach (var n in d.Nodes.Where(n => n.IsActivityish))
                {
                    string nt = n.Kind switch
                    {
                        NodeKind.Initial => "uml:InitialNode",
                        NodeKind.ActivityFinal => "uml:ActivityFinalNode",
                        NodeKind.FlowFinal => "uml:FlowFinalNode",
                        NodeKind.Action => "uml:OpaqueAction",
                        NodeKind.Decision => "uml:DecisionNode",
                        NodeKind.Fork => "uml:ForkNode",
                        NodeKind.SendSignal => "uml:SendSignalAction",
                        NodeKind.AcceptSignal => "uml:AcceptEventAction",
                        NodeKind.TimeEvent => "uml:AcceptEventAction",
                        _ => "uml:ObjectNode"
                    };
                    sb.AppendLine($"        <node xmi:type=\"{nt}\" xmi:id=\"{n.Id}\" name=\"{X(n.Name)}\"/>");
                }
                foreach (var e in d.Edges.Where(e => e.Kind is EdgeKind.ControlFlow or EdgeKind.ObjectFlow))
                {
                    sb.AppendLine($"        <edge xmi:type=\"uml:{(e.Kind == EdgeKind.ObjectFlow ? "ObjectFlow" : "ControlFlow")}\" xmi:id=\"{e.Id}\" source=\"{e.SourceId}\" target=\"{e.TargetId}\">");
                    if (!string.IsNullOrWhiteSpace(e.Guard)) sb.AppendLine($"          <guard xmi:type=\"uml:OpaqueExpression\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(e.Guard)}</body></guard>");
                    sb.AppendLine("        </edge>");
                }
                sb.AppendLine("      </packagedElement>");
            }
            sb.AppendLine("    </packagedElement>");
        }
        foreach (var p in primitives.OrderBy(x => x))
            sb.AppendLine($"    <packagedElement xmi:type=\"uml:PrimitiveType\" xmi:id=\"_prim_{p}\" name=\"{X(p)}\"/>");
        sb.AppendLine("  </uml:Model>");
        sb.AppendLine("</xmi:XMI>");
        File.WriteAllText(path, sb.ToString());
    }
}