using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UmlForge;

public static class ProjectStore
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UmlForge");
    public static string AutosavePath => Path.Combine(Dir, "autosave.uforge");
    public static string BackupPath => Path.Combine(Dir, "autosave.bak");
    public static string SettingsPath => Path.Combine(Dir, "settings.json");

    public static void EnsureDir() => Directory.CreateDirectory(Dir);

    public static void Save(Project p, string path)
    {
        EnsureDir();
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(p, Editor.Json));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
    public static Project Load(string path) => JsonSerializer.Deserialize<Project>(File.ReadAllText(path), Editor.Json);

    public static void Autosave(Project p)
    {
        try
        {
            EnsureDir();
            if (File.Exists(AutosavePath)) File.Copy(AutosavePath, BackupPath, true);
            File.WriteAllText(AutosavePath, JsonSerializer.Serialize(p, Editor.Json));
        }
        catch { /* never crash on autosave */ }
    }
    public static Project LoadAutosave()
    {
        foreach (var path in new[] { AutosavePath, BackupPath })
        {
            try { if (File.Exists(path)) return Load(path); } catch { }
        }
        return null;
    }
    public static AppSettings LoadSettings()
    {
        try { if (File.Exists(SettingsPath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Editor.Json); } catch { }
        return new AppSettings();
    }
    public static void SaveSettings(AppSettings s)
    {
        try { EnsureDir(); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, Editor.Json)); } catch { }
    }
}

public static class FeatureParser
{
    public static object Parse(string input)
    {
        string s = (input ?? "").Trim();
        if (s.Length == 0) return null;
        var vis = Visibility.Public;
        if (s.Length > 0 && "+-#~".Contains(s[0]))
        {
            vis = s[0] switch { '+' => Visibility.Public, '-' => Visibility.Private, '#' => Visibility.Protected, _ => Visibility.Package };
            s = s[1..].Trim();
        }
        bool isStatic = false;
        if (s.StartsWith("static ", StringComparison.OrdinalIgnoreCase)) { isStatic = true; s = s[7..].Trim(); }
        bool ro = s.Contains("readOnly"); isStatic |= s.Contains("{static}") || System.Text.RegularExpressions.Regex.IsMatch(s, @"\{[^}]*static[^}]*\}");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\{[^}]*\}", "").Trim();
        string def = null;
        var dm = System.Text.RegularExpressions.Regex.Match(s, @"=\s*(.+)$");
        if (dm.Success) { def = dm.Groups[1].Value.Trim(); s = s[..dm.Index].Trim(); }
        string mult = null;
        var mm = System.Text.RegularExpressions.Regex.Match(s, @"\[([^\]]*)\]\s*$");
        if (mm.Success) { mult = mm.Groups[1].Value.Trim(); s = s[..mm.Index].Trim(); }
        string type = null;
        var tm = System.Text.RegularExpressions.Regex.Match(s, @":\s*([^:]+)$");
        if (tm.Success) { type = tm.Groups[1].Value.Trim(); s = s[..tm.Index].Trim(); }
        var pm = System.Text.RegularExpressions.Regex.Match(s, @"^(\w+)\s*\((.*)\)\s*$");
        if (pm.Success)
            return new UmlOperation { Visibility = vis, Name = pm.Groups[1].Value, Parameters = pm.Groups[2].Value.Trim(), ReturnType = type, IsStatic = isStatic };
        bool derived = s.StartsWith("/");
        return new UmlAttribute { Visibility = vis, Name = derived ? s[1..] : s, Type = type, Multiplicity = mult, Default = def, IsStatic = isStatic, IsReadOnly = ro, IsDerived = derived };
    }

    public static void ParseTransition(UmlEdge e, string label)
    {
        string s = label ?? "";
        var gm = System.Text.RegularExpressions.Regex.Match(s, @"\[([^\]]*)\]");
        e.Guard = gm.Success ? gm.Groups[1].Value.Trim() : "";
        if (gm.Success) s = s.Remove(gm.Index, gm.Length);
        int slash = s.IndexOf('/');
        if (slash >= 0) { e.Effect = s[(slash + 1)..].Trim(); e.Trigger = s[..slash].Trim(); }
        else { e.Trigger = s.Trim(); e.Effect = ""; }
    }
}

public static class ValidationService
{
    public static List<Issue> Validate(Diagram d)
    {
        var issues = new List<Issue>();
        void Add(Severity sev, string msg, string id) => issues.Add(new Issue { Severity = sev, Message = msg, ElementId = id });

        var named = d.Nodes.Where(n => n.Kind is not (NodeKind.Initial or NodeKind.StateInitial or NodeKind.ActivityFinal or NodeKind.StateFinal
            or NodeKind.FlowFinal or NodeKind.Junction or NodeKind.Terminate or NodeKind.Choice or NodeKind.Decision or NodeKind.Fork
            or NodeKind.HistoryShallow or NodeKind.HistoryDeep or NodeKind.Port)).ToList();
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

public static class PngExporter
{
    public static void Export(Diagram d, AppSettings settings, string path, double scale = 2)
    {
        var theme = Theme.For(settings.Paper);
        var bounds = Geo.ContentBounds(d, settings.FontSize);
        var st = new RenderState { Theme = theme, FontSize = settings.FontSize, Export = true, Shadows = false };
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(theme.Paper, null, bounds);
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            var r = new Renderer(dc, st);
            foreach (var e in d.Edges) r.DrawEdge(e, d);
            foreach (var n in d.Nodes) r.DrawNode(n);
            dc.Pop();
        }
        int w = (int)Math.Ceiling(bounds.Width * scale), h = (int)Math.Ceiling(bounds.Height * scale);
        var rtb = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path); enc.Save(fs);
    }
}

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
            string key = n.Kind + "|" + n.Name;
            if (ids.TryGetValue(key, out var id)) return id;
            id = "_" + Guid.NewGuid().ToString("N")[..12]; ids[key] = id; return id;
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
                            NodeKind.Class => "uml:Class", NodeKind.Interface => "uml:Interface", NodeKind.DataType => "uml:DataType",
                            NodeKind.Enumeration => "uml:Enumeration", NodeKind.Component => "uml:Component", NodeKind.Actor => "uml:Actor",
                            NodeKind.UseCase => "uml:UseCase", NodeKind.Package => "uml:Package", NodeKind.DeviceNode => "uml:Device",
                            NodeKind.ExecEnvNode => "uml:ExecutionEnvironment", _ => "uml:Artifact"
                        };
                        sb.AppendLine($"      <packagedElement xmi:type=\"{type}\" xmi:id=\"{id}\" name=\"{X(n.Name)}\"{(n.IsAbstract ? " isAbstract=\"true\"" : "")}>");
                        foreach (var a in n.Attributes)
                        {
                            string tr = TypeRef(a.Type);
                            sb.AppendLine($"        <ownedAttribute xmi:type=\"uml:Property\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(a.Name)}\" visibility=\"{n.Visibility.ToString().ToLower()}\"{(a.IsStatic ? " isStatic=\"true\"" : "")}{(a.IsReadOnly ? " isReadOnly=\"true\"" : "")}{(tr != null ? $" type=\"{tr}\"" : "")}>");
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
            // Associations
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
            // Dependencies
            foreach (var d in pkg.Diagrams)
                foreach (var e in d.Edges.Where(e => e.Kind is EdgeKind.Dependency or EdgeKind.Usage or EdgeKind.Deployment or EdgeKind.Manifest or EdgeKind.PackageImport or EdgeKind.PackageMerge))
                {
                    var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
                    if (s == null || t == null) continue;
                    sb.AppendLine($"      <packagedElement xmi:type=\"uml:Dependency\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"{X(e.Name)}\" client=\"{IdOf(s)}\" supplier=\"{IdOf(t)}\"/>");
                }
            // State machines
            foreach (var d in pkg.Diagrams.Where(d => d.Kind == DiagramKind.StateMachine))
            {
                string smId = "_" + Guid.NewGuid().ToString("N")[..12];
                sb.AppendLine($"      <packagedElement xmi:type=\"uml:StateMachine\" xmi:id=\"{smId}\" name=\"{X(d.Name)}\">");
                sb.AppendLine($"        <region xmi:type=\"uml:Region\" xmi:id=\"_{Guid.NewGuid():N}\" name=\"Region\">");
                foreach (var n in d.Nodes.Where(n => n.IsStateish))
                {
                    string vt = n.Kind switch
                    {
                        NodeKind.State => "uml:State", NodeKind.StateInitial => "uml:Pseudostate", NodeKind.StateFinal => "uml:FinalState",
                        NodeKind.Choice or NodeKind.Junction or NodeKind.Terminate or NodeKind.HistoryShallow or NodeKind.HistoryDeep => "uml:Pseudostate", _ => "uml:State"
                    };
                    string kindAttr = n.Kind switch
                    {
                        NodeKind.StateInitial => " kind=\"initial\"", NodeKind.Choice => " kind=\"choice\"", NodeKind.Junction => " kind=\"junction\"",
                        NodeKind.Terminate => " kind=\"terminate\"", NodeKind.HistoryShallow => " kind=\"shallowHistory\"", NodeKind.HistoryDeep => " kind=\"deepHistory\"", _ => ""
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
                    if (!string.IsNullOrWhiteSpace(e.Guard)) sb.AppendLine($"            <guard xmi:type=\"uml:OpaqueExpression\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(e.Guard)}</body></guard>");
                    if (!string.IsNullOrWhiteSpace(e.Effect)) sb.AppendLine($"            <effect xmi:type=\"uml:OpaqueBehavior\" xmi:id=\"_{Guid.NewGuid():N}\"><body>{X(e.Effect)}</body></effect>");
                    sb.AppendLine("          </transition>");
                }
                sb.AppendLine("        </region></packagedElement>");
            }
            // Activities
            foreach (var d in pkg.Diagrams.Where(d => d.Kind == DiagramKind.Activity))
            {
                string aid = "_" + Guid.NewGuid().ToString("N")[..12];
                sb.AppendLine($"      <packagedElement xmi:type=\"uml:Activity\" xmi:id=\"{aid}\" name=\"{X(d.Name)}\">");
                foreach (var n in d.Nodes.Where(n => n.IsActivityish))
                {
                    string nt = n.Kind switch
                    {
                        NodeKind.Initial => "uml:InitialNode", NodeKind.ActivityFinal => "uml:ActivityFinalNode", NodeKind.FlowFinal => "uml:FlowFinalNode",
                        NodeKind.Action => "uml:OpaqueAction", NodeKind.Decision => "uml:DecisionNode", NodeKind.Fork => "uml:ForkNode",
                        NodeKind.SendSignal => "uml:SendSignalAction", NodeKind.AcceptSignal => "uml:AcceptEventAction",
                        NodeKind.TimeEvent => "uml:AcceptEventAction", _ => "uml:ObjectNode"
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

public static class Seed
{
    static UmlNode N(NodeKind k, double x, double y, double w, double h, string name, Action<UmlNode> extra = null)
    {
        var n = new UmlNode { Kind = k, X = x, Y = y, W = w, H = h, Name = name };
        extra?.Invoke(n); return n;
    }
    static UmlEdge E(EdgeKind k, UmlNode s, UmlNode t, Action<UmlEdge> extra = null)
    {
        var e = new UmlEdge { Kind = k, SourceId = s.Id, TargetId = t.Id };
        extra?.Invoke(e); return e;
    }
    static UmlAttribute A(string name, string type, Visibility v = Visibility.Private) => new() { Name = name, Type = type, Visibility = v };
    static UmlOperation O(string name, string ret, string ps = "", Visibility v = Visibility.Public) => new() { Name = name, ReturnType = ret, Parameters = ps, Visibility = v };

    public static Project Build()
    {
        var p = new Project { Name = "Library Management System" };
        var pkg = new UmlPackage { Name = "LibraryModel" };
        p.Packages.Add(pkg);

        // ---------- Class ----------
        var cd = new Diagram { Kind = DiagramKind.Class, Name = "Domain Model" };
        var searchable = N(NodeKind.Interface, 60, 60, 200, 80, "Searchable", n => n.Operations.Add(O("search", "Book[*]", "query : String")));
        var catalog = N(NodeKind.Class, 60, 220, 230, 110, "Catalog", n => { n.Operations.Add(O("search", "Book[*]", "title : String")); n.Operations.Add(O("addBook", "", "b : Book")); });
        var library = N(NodeKind.Class, 60, 470, 230, 130, "Library", n => { n.Attributes.Add(A("name", "String")); n.Attributes.Add(A("address", "String")); n.Operations.Add(O("registerMember", "Member", "name : String")); });
        var book = N(NodeKind.Class, 450, 110, 230, 140, "Book", n => { n.Attributes.Add(A("title", "String")); n.Attributes.Add(A("ISBN", "String")); n.Attributes.Add(A("publicationYear", "Integer")); n.Operations.Add(O("isAvailable", "Boolean")); });
        var ebook = N(NodeKind.Class, 380, 420, 200, 100, "EBook", n => { n.Attributes.Add(A("fileFormat", "String")); n.Operations.Add(O("download", "Boolean")); });
        var printed = N(NodeKind.Class, 620, 420, 210, 100, "PrintedBook", n => { n.Attributes.Add(A("shelfLocation", "String")); n.Attributes.Add(A("pageCount", "Integer")); });
        var member = N(NodeKind.Class, 920, 110, 240, 140, "Member", n => { n.IsAbstract = true; n.Attributes.Add(A("name", "String")); n.Attributes.Add(A("memberId", "String")); n.Operations.Add(new UmlOperation { Name = "maxLoans", ReturnType = "Integer", IsAbstract = true }); });
        var student = N(NodeKind.Class, 850, 420, 210, 100, "StudentMember", n => { n.Attributes.Add(A("university", "String")); n.Operations.Add(O("maxLoans", "Integer")); });
        var staff = N(NodeKind.Class, 1100, 420, 210, 100, "StaffMember", n => { n.Attributes.Add(A("department", "String")); n.Operations.Add(O("maxLoans", "Integer")); });
        var loan = N(NodeKind.Class, 450, 660, 240, 140, "Loan", n => { n.Attributes.Add(A("loanDate", "Date")); n.Attributes.Add(A("dueDate", "Date")); n.Attributes.Add(A("returnDate", "Date")); n.Operations.Add(O("renew", "Boolean")); });
        var reservation = N(NodeKind.Class, 780, 660, 230, 110, "Reservation", n => { n.Attributes.Add(A("reservedDate", "Date")); n.Attributes.Add(A("expiryDate", "Date")); });
        var status = N(NodeKind.Enumeration, 1100, 660, 170, 110, "LoanStatus", n => { n.Literals.Add("ACTIVE"); n.Literals.Add("OVERDUE"); n.Literals.Add("RETURNED"); });
        var note = N(NodeKind.Note, 1100, 840, 240, 80, "A loan becomes OVERDUE when dueDate < today.");
        cd.Nodes.AddRange(searchable, catalog, library, book, ebook, printed, member, student, staff, loan, reservation, status, note);
        cd.Edges.AddRange(
            E(EdgeKind.Realization, catalog, searchable),
            E(EdgeKind.Composition, library, catalog, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Composition, library, book, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "holdings"; }),
            E(EdgeKind.Association, catalog, book, e => { e.Name = "indexes"; e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; }),
            E(EdgeKind.Composition, library, member, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "members"; }),
            E(EdgeKind.Association, member, loan, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "loans"; }),
            E(EdgeKind.Association, loan, book, e => { e.SourceMultiplicity = "0..*"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Association, member, reservation, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; }),
            E(EdgeKind.Association, reservation, book, e => { e.SourceMultiplicity = "0..*"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Generalization, ebook, book),
            E(EdgeKind.Generalization, printed, book),
            E(EdgeKind.Generalization, student, member),
            E(EdgeKind.Generalization, staff, member),
            E(EdgeKind.Usage, loan, status),
            E(EdgeKind.NoteLink, note, loan));
        pkg.Diagrams.Add(cd);

        // ---------- Use case ----------
        var ud = new Diagram { Kind = DiagramKind.UseCase, Name = "Borrowing Services" };
        var boundary = N(NodeKind.Boundary, 250, 50, 720, 560, "Library System");
        var mActor = N(NodeKind.Actor, 70, 240, 64, 96, "Member");
        var lActor = N(NodeKind.Actor, 1060, 260, 64, 96, "Librarian");
        var search = N(NodeKind.UseCase, 320, 120, 160, 72, "Search Catalog");
        var borrow = N(NodeKind.UseCase, 320, 270, 160, 72, "Borrow Book");
        var ret = N(NodeKind.UseCase, 320, 420, 160, 72, "Return Book");
        var reserve = N(NodeKind.UseCase, 570, 120, 160, 72, "Reserve Book");
        var verify = N(NodeKind.UseCase, 570, 270, 170, 72, "Verify Availability");
        var fine = N(NodeKind.UseCase, 570, 420, 160, 72, "Pay Fine");
        var manage = N(NodeKind.UseCase, 790, 180, 160, 72, "Manage Members");
        var notice = N(NodeKind.UseCase, 790, 350, 170, 72, "Send Overdue Notice");
        ud.Nodes.AddRange(boundary, mActor, lActor, search, borrow, ret, reserve, verify, fine, manage, notice);
        ud.Edges.AddRange(
            E(EdgeKind.CommunicationAssociation, mActor, search), E(EdgeKind.CommunicationAssociation, mActor, borrow),
            E(EdgeKind.CommunicationAssociation, mActor, ret), E(EdgeKind.CommunicationAssociation, mActor, reserve),
            E(EdgeKind.CommunicationAssociation, lActor, manage), E(EdgeKind.CommunicationAssociation, lActor, notice),
            E(EdgeKind.Include, borrow, verify), E(EdgeKind.Include, reserve, search), E(EdgeKind.Extend, fine, ret));
        pkg.Diagrams.Add(ud);

        // ---------- Sequence ----------
        var sd = new Diagram { Kind = DiagramKind.Sequence, Name = "borrowBook Flow" };
        var lm = N(NodeKind.Lifeline, 60, 60, 130, 560, "m", n => n.Represents = "Member");
        var lui = N(NodeKind.Lifeline, 260, 60, 140, 560, "ui", n => n.Represents = "LoanScreen");
        var lc = N(NodeKind.Lifeline, 470, 60, 160, 560, "ctrl", n => n.Represents = "LoanController");
        var lcat = N(NodeKind.Lifeline, 700, 60, 140, 560, "cat", n => n.Represents = "Catalog");
        var lb = N(NodeKind.Lifeline, 910, 60, 130, 560, "b", n => n.Represents = "Book");
        var ll = N(NodeKind.Lifeline, 1120, 300, 130, 320, "loan", n => n.Represents = "Loan");
        var frag = N(NodeKind.Fragment, 430, 330, 760, 250, "", n => { n.Operator = "alt"; n.Operands.Add(new FragmentOperand { Guard = "book available", Height = 150 }); n.Operands.Add(new FragmentOperand { Guard = "else", Height = 100 }); });
        sd.Nodes.AddRange(frag, lm, lui, lc, lcat, lb, ll);
        void Msg(EdgeKind k, UmlNode s, UmlNode t, double y, string name)
        {
            var e = E(k, s, t, x => { x.Y = y; x.Name = name; });
            sd.Edges.Add(e);
            if (k == EdgeKind.MessageSync && s != t) t.Activations.Add(new Activation { Start = y, End = y + 44, MessageId = e.Id });
        }
        Msg(EdgeKind.MessageSync, lm, lui, 140, "borrowBook(ISBN)");
        Msg(EdgeKind.MessageSync, lui, lc, 185, "borrow(m, ISBN)");
        Msg(EdgeKind.MessageSync, lc, lcat, 230, "findBook(ISBN)");
        Msg(EdgeKind.MessageReply, lcat, lc, 270, "book");
        Msg(EdgeKind.MessageCreate, lc, ll, 318, "new Loan(m, b)");
        Msg(EdgeKind.MessageSync, lc, lb, 380, "markOnLoan()");
        Msg(EdgeKind.MessageReply, lb, lc, 415, "ok");
        Msg(EdgeKind.MessageReply, lc, lui, 450, "confirm");
        Msg(EdgeKind.MessageReply, lui, lm, 485, "receipt");
        Msg(EdgeKind.MessageReply, lc, lui, 545, "unavailable");
        pkg.Diagrams.Add(sd);

        // ---------- Activity ----------
        var ad = new Diagram { Kind = DiagramKind.Activity, Name = "Return Book" };
        var init = N(NodeKind.Initial, 70, 205, 20, 20, "");
        var accept = N(NodeKind.Action, 140, 190, 150, 52, "Accept Return");
        var dec = N(NodeKind.Decision, 350, 193, 46, 46, "Overdue?");
        var calc = N(NodeKind.Action, 460, 100, 150, 52, "Calculate Fine");
        var pay = N(NodeKind.Action, 660, 100, 150, 52, "Accept Payment");
        var merge = N(NodeKind.Decision, 860, 193, 46, 46, "");
        var shelve = N(NodeKind.Action, 960, 190, 150, 52, "Shelve Book");
        var fork = N(NodeKind.Fork, 1160, 212, 90, 8, "");
        var upd = N(NodeKind.Action, 1130, 90, 160, 52, "Update Catalog");
        var notify = N(NodeKind.Action, 1130, 300, 160, 52, "Notify Member");
        var join = N(NodeKind.Fork, 1350, 212, 90, 8, "");
        var final = N(NodeKind.ActivityFinal, 1490, 203, 26, 26, "");
        ad.Nodes.AddRange(init, accept, dec, calc, pay, merge, shelve, fork, upd, notify, join, final);
        ad.Edges.AddRange(
            E(EdgeKind.ControlFlow, init, accept), E(EdgeKind.ControlFlow, accept, dec),
            E(EdgeKind.ControlFlow, dec, calc, e => e.Guard = "yes"), E(EdgeKind.ControlFlow, calc, pay),
            E(EdgeKind.ControlFlow, pay, merge), E(EdgeKind.ControlFlow, dec, merge, e => e.Guard = "no"),
            E(EdgeKind.ControlFlow, merge, shelve), E(EdgeKind.ControlFlow, shelve, fork),
            E(EdgeKind.ControlFlow, fork, upd), E(EdgeKind.ControlFlow, fork, notify),
            E(EdgeKind.ControlFlow, upd, join), E(EdgeKind.ControlFlow, notify, join),
            E(EdgeKind.ControlFlow, join, final));
        pkg.Diagrams.Add(ad);

        // ---------- State machine ----------
        var sm = new Diagram { Kind = DiagramKind.StateMachine, Name = "Book Lifecycle" };
        var si = N(NodeKind.StateInitial, 70, 165, 20, 20, "");
        var avail = N(NodeKind.State, 150, 140, 160, 64, "Available");
        var onloan = N(NodeKind.State, 430, 120, 210, 110, "OnLoan", n => { n.Entry = "startTimer"; n.Do = "trackDueDate"; n.Exit = "stopTimer"; });
        var reserved = N(NodeKind.State, 430, 340, 160, 64, "Reserved");
        var lost = N(NodeKind.State, 760, 140, 150, 64, "Lost");
        var sf = N(NodeKind.StateFinal, 990, 160, 26, 26, "");
        sm.Nodes.AddRange(si, avail, onloan, reserved, lost, sf);
        sm.Edges.AddRange(
            E(EdgeKind.Transition, si, avail),
            E(EdgeKind.Transition, avail, onloan, e => { e.Trigger = "borrow"; e.Effect = "markOnLoan"; }),
            E(EdgeKind.Transition, onloan, avail, e => { e.Trigger = "return"; e.Effect = "markAvailable"; e.Routing = Routing.Orthogonal; e.BendOffset = -40; }),
            E(EdgeKind.Transition, onloan, onloan, e => { e.Trigger = "renew"; e.Guard = "renewals < 2"; e.Effect = "extendDueDate"; }),
            E(EdgeKind.Transition, avail, reserved, e => { e.Trigger = "reserve"; e.Routing = Routing.Orthogonal; }),
            E(EdgeKind.Transition, reserved, onloan, e => { e.Trigger = "borrow"; }),
            E(EdgeKind.Transition, reserved, avail, e => { e.Trigger = "cancel"; e.Routing = Routing.Orthogonal; e.BendOffset = 40; }),
            E(EdgeKind.Transition, onloan, lost, e => { e.Trigger = "reportLost"; }),
            E(EdgeKind.Transition, lost, avail, e => { e.Trigger = "found"; e.Routing = Routing.Orthogonal; e.BendOffset = -70; }),
            E(EdgeKind.Transition, lost, sf, e => { e.Trigger = "discard"; }));
        pkg.Diagrams.Add(sm);

        return p;
    }
}

public static class CollectionExtensions
{
    public static void AddRange<T>(this System.Collections.ObjectModel.ObservableCollection<T> c, params T[] items)
    { foreach (var i in items) c.Add(i); }
}