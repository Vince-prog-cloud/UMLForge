namespace UmlForge;

public enum DiagramKind { Class, Object, UseCase, Sequence, Activity, StateMachine, Component, Deployment, Package }
public enum Visibility { Public, Private, Protected, Package }
public enum Routing { Straight, Orthogonal }
public enum PaperMode { White, Cream, Blueprint }
public enum Severity { Info, Warning, Error }
public enum ToolMode { Select, Pan, Place, Connect }

public enum NodeKind
{
    Class, Interface, Enumeration, DataType, Object,
    Actor, UseCase, Boundary,
    Lifeline, Fragment, InteractionUse,
    Initial, ActivityFinal, FlowFinal, Action, Decision, Fork, SendSignal, AcceptSignal, TimeEvent, ObjectNode, Partition,
    State, StateInitial, StateFinal, Choice, Junction, Terminate, HistoryShallow, HistoryDeep,
    Component, Port, ProvidedInterface, RequiredInterface,
    DeviceNode, ExecEnvNode, Artifact, Package, Note
}

public enum EdgeKind
{
    Association, Aggregation, Composition, Generalization, Realization, Dependency, Usage,
    Include, Extend, Link, CommunicationAssociation,
    ControlFlow, ObjectFlow, Transition,
    MessageSync, MessageAsync, MessageReply, MessageCreate, MessageDelete,
    CommunicationPath, Deployment, Manifest, PackageImport, PackageMerge, NoteLink
}

public enum MarkerKind { None, Open, Triangle, FilledTriangle, DiamondHollow, DiamondFilled, Cross }