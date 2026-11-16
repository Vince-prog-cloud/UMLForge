namespace UmlForge;

public static class ConnectionRules
{
    public static (bool ok, string reason) CanConnect(EdgeKind kind, UmlNode s, UmlNode t, DiagramKind diagram)
    {
        if (s == null || t == null) return (false, "Drop on an element.");
        if (kind == EdgeKind.NoteLink)
            return (s.Kind == NodeKind.Note || t.Kind == NodeKind.Note) ? (true, "") : (false, "A note link must touch a note.");
        if (s.Kind == NodeKind.Note || t.Kind == NodeKind.Note) return (false, "Use the Note Link tool for notes.");

        switch (kind)
        {
            case EdgeKind.Generalization:
                if (s == t) return (false, "An element cannot generalize itself.");
                bool okG = (s.IsClassifier && t.IsClassifier) || (s.Kind == NodeKind.Actor && t.Kind == NodeKind.Actor)
                    || (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.UseCase) || (s.Kind == NodeKind.Component && t.Kind == NodeKind.Component)
                    || (s.Kind == NodeKind.State && t.Kind == NodeKind.State)
                    || (s.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode);
                return okG ? (true, "") : (false, "Generalization needs two classifiers of the same category (child → parent).");
            case EdgeKind.Realization:
                if (t.Kind == NodeKind.Interface && (s.Kind is NodeKind.Class or NodeKind.Component or NodeKind.DataType)) return (true, "");
                return (false, "Realization: classifier → interface.");
            case EdgeKind.Aggregation or EdgeKind.Composition:
                if (s == t) return (false, "Whole and part must differ.");
                return (s.IsClassifier && t.IsClassifier) ? (true, "") : (false, "Aggregation / composition connects classifiers (whole → part).");
            case EdgeKind.Association:
                if (diagram == DiagramKind.Component) return (true, "");
                return (s.IsClassifier && t.IsClassifier) ? (true, "") : (false, "Association connects classifiers.");
            case EdgeKind.Link:
                return (s.Kind == NodeKind.Object && t.Kind == NodeKind.Object) ? (true, "") : (false, "Links connect objects.");
            case EdgeKind.CommunicationAssociation:
                bool uc = (s.Kind == NodeKind.Actor && t.Kind == NodeKind.UseCase) || (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.Actor);
                return uc ? (true, "") : (false, "Communication association: actor ↔ use case.");
            case EdgeKind.Include or EdgeKind.Extend:
                return (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.UseCase && s != t) ? (true, "") : (false, $"{kind} connects two different use cases.");
            case EdgeKind.ControlFlow or EdgeKind.ObjectFlow:
                return (s.IsActivityish && t.IsActivityish) ? (true, "") : (false, "Flows connect activity nodes.");
            case EdgeKind.Transition:
                return (s.IsStateish && t.IsStateish) ? (true, "") : (false, "Transitions connect states / pseudostates.");
            case EdgeKind.MessageSync or EdgeKind.MessageAsync or EdgeKind.MessageReply or EdgeKind.MessageCreate or EdgeKind.MessageDelete:
                if (s.Kind != NodeKind.Lifeline || t.Kind != NodeKind.Lifeline) return (false, "Messages connect lifelines.");
                if (kind == EdgeKind.MessageCreate && s == t) return (false, "Create must target another lifeline.");
                return (true, "");
            case EdgeKind.CommunicationPath:
                return (s.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode)
                    ? (true, "") : (false, "Communication path connects deployment nodes.");
            case EdgeKind.Deployment:
                return (s.Kind is NodeKind.Artifact or NodeKind.Component && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode)
                    ? (true, "") : (false, "«deploy»: artifact / component → node.");
            case EdgeKind.Manifest:
                return (s.Kind == NodeKind.Artifact && t.Kind == NodeKind.Component) ? (true, "") : (false, "«manifest»: artifact → component.");
            case EdgeKind.PackageImport or EdgeKind.PackageMerge:
                return (s.Kind == NodeKind.Package && t.Kind == NodeKind.Package && s != t) ? (true, "") : (false, "Import / merge connects two packages.");
            default:
                return (true, "");
        }
    }
}