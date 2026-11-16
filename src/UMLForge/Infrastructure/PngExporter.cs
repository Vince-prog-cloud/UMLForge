using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UmlForge;

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