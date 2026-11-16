namespace UmlForge;

public record EdgeDef(string Label, bool Dashed, MarkerKind Start, MarkerKind End, string Stereo = "", string Hint = "");

public static class EdgeCatalog
{
    public static readonly Dictionary<EdgeKind, EdgeDef> Edges = new()
    {
        [EdgeKind.Association] = new("Association", false, MarkerKind.None, MarkerKind.None, Hint: "Plain line; add role names / multiplicities in Properties."),
        [EdgeKind.Aggregation] = new("Aggregation", false, MarkerKind.DiamondHollow, MarkerKind.None, Hint: "Drag from whole → part. Hollow diamond at the whole."),
        [EdgeKind.Composition] = new("Composition", false, MarkerKind.DiamondFilled, MarkerKind.None, Hint: "Drag from whole → part. Filled diamond; exclusive ownership."),
        [EdgeKind.Generalization] = new("Generalization", false, MarkerKind.None, MarkerKind.Triangle, Hint: "Drag child → parent. Hollow triangle at parent."),
        [EdgeKind.Realization] = new("Realization", true, MarkerKind.None, MarkerKind.Triangle, Hint: "Drag classifier → interface. Dashed + hollow triangle."),
        [EdgeKind.Dependency] = new("Dependency", true, MarkerKind.None, MarkerKind.Open, Hint: "Drag client → supplier. Dashed + open arrow."),
        [EdgeKind.Usage] = new("Usage", true, MarkerKind.None, MarkerKind.Open, "use"),
        [EdgeKind.Include] = new("Include", true, MarkerKind.None, MarkerKind.Open, "include", "Base use case → included use case."),
        [EdgeKind.Extend] = new("Extend", true, MarkerKind.None, MarkerKind.Open, "extend", "Extension use case → base use case."),
        [EdgeKind.Link] = new("Link", false, MarkerKind.None, MarkerKind.None, Hint: "Instance-level association."),
        [EdgeKind.CommunicationAssociation] = new("Communication", false, MarkerKind.None, MarkerKind.None, Hint: "Actor ↔ use case, no arrowheads."),
        [EdgeKind.ControlFlow] = new("Control Flow", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.ObjectFlow] = new("Object Flow", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.Transition] = new("Transition", false, MarkerKind.None, MarkerKind.Open, Hint: "Label: trigger [guard] / effect."),
        [EdgeKind.MessageSync] = new("Synchronous Message", false, MarkerKind.None, MarkerKind.FilledTriangle, Hint: "Solid line, filled arrowhead. Creates an activation."),
        [EdgeKind.MessageAsync] = new("Asynchronous Message", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.MessageReply] = new("Reply Message", true, MarkerKind.None, MarkerKind.Open, Hint: "Dashed, open arrowhead. Closes the activation."),
        [EdgeKind.MessageCreate] = new("Create Message", true, MarkerKind.None, MarkerKind.Open, "create", "Points at the created lifeline's head."),
        [EdgeKind.MessageDelete] = new("Delete Message", false, MarkerKind.None, MarkerKind.Cross, Hint: "X destroys the target lifeline."),
        [EdgeKind.CommunicationPath] = new("Communication Path", false, MarkerKind.None, MarkerKind.None, Hint: "Node ↔ node association."),
        [EdgeKind.Deployment] = new("Deployment", true, MarkerKind.None, MarkerKind.Open, "deploy", "Artifact → node."),
        [EdgeKind.Manifest] = new("Manifestation", true, MarkerKind.None, MarkerKind.Open, "manifest", "Artifact → component."),
        [EdgeKind.PackageImport] = new("Package Import", true, MarkerKind.None, MarkerKind.Open, "import"),
        [EdgeKind.PackageMerge] = new("Package Merge", true, MarkerKind.None, MarkerKind.Open, "merge"),
        [EdgeKind.NoteLink] = new("Note Link", true, MarkerKind.None, MarkerKind.None),
    };
}