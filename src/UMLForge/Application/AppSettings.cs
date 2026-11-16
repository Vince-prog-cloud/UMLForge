namespace UmlForge;

public class AppSettings
{
    public PaperMode Paper { get; set; } = PaperMode.White;
    public bool Grid { get; set; } = true;
    public bool Snap { get; set; } = true;
    public bool Guides { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public double GridSize { get; set; } = 20;
    public double FontSize { get; set; } = 13;
    public Routing DefaultRouting { get; set; } = Routing.Straight;
    public string LastFile { get; set; } = "";
}

public struct ToolSpec
{
    public ToolMode Mode;
    public NodeKind Node;
    public EdgeKind Edge;

    public static ToolSpec Select => new() { Mode = ToolMode.Select };
    public static ToolSpec Pan => new() { Mode = ToolMode.Pan };
    public static ToolSpec Place(NodeKind k) => new() { Mode = ToolMode.Place, Node = k };
    public static ToolSpec Connect(EdgeKind k) => new() { Mode = ToolMode.Connect, Edge = k };
}