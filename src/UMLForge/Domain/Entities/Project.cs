using System.Collections.ObjectModel;

namespace UmlForge;

public class Diagram : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    DiagramKind _kind;
    string _name = "Diagram", _desc = "";
    bool _seqNumbers = true;

    public DiagramKind Kind { get => _kind; set => Set(ref _kind, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Description { get => _desc; set => Set(ref _desc, value); }
    public bool ShowSequenceNumbers { get => _seqNumbers; set => Set(ref _seqNumbers, value); }
    public ObservableCollection<UmlNode> Nodes { get; set; } = new();
    public ObservableCollection<UmlEdge> Edges { get; set; } = new();

    public UmlNode Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public UmlEdge Edge(string id) => Edges.FirstOrDefault(e => e.Id == id);
}

public class UmlPackage : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    string _name = "Package";
    public string Name { get => _name; set => Set(ref _name, value); }
    public ObservableCollection<Diagram> Diagrams { get; set; } = new();
}

public class Project : Observable
{
    public int SchemaVersion { get; set; } = 1;
    string _name = "Untitled Project";
    public string Name { get => _name; set => Set(ref _name, value); }
    public ObservableCollection<UmlPackage> Packages { get; set; } = new();

    public IEnumerable<Diagram> AllDiagrams => Packages.SelectMany(p => p.Diagrams);
    public Diagram FindDiagram(string id) => AllDiagrams.FirstOrDefault(d => d.Id == id);
    public UmlPackage PackageOf(Diagram d) => Packages.FirstOrDefault(p => p.Diagrams.Contains(d));
}

public class Issue
{
    public Severity Severity { get; set; }
    public string Message { get; set; } = "";
    public string ElementId { get; set; } = "";
}