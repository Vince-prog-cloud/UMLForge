using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class DrawingPrimitives
{
    public readonly DrawingContext Dc;
    public readonly RenderState State;
    public readonly Theme Theme;
    public readonly double Fs;

    public DrawingPrimitives(DrawingContext dc, RenderState st)
    {
        Dc = dc;
        State = st;
        Theme = st.Theme;
        Fs = st.FontSize;
    }

    public void Rect(Rect r, Brush fill, Pen pen) => Dc.DrawRectangle(fill, pen, r);
    public void Round(Rect r, double rad, Brush fill, Pen pen) => Dc.DrawRoundedRectangle(fill, pen, r, rad, rad);
    public void Ellipse(Point c, double rx, double ry, Brush fill, Pen pen) => Dc.DrawEllipse(fill, pen, c, rx, ry);
    public void Line(Point a, Point b, Pen pen) => Dc.DrawLine(pen, a, b);

    public void Poly(IEnumerable<Point> pts, Brush fill, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            bool first = true;
            foreach (var p in pts)
            {
                if (first) { c.BeginFigure(p, fill != null, true); first = false; }
                else c.LineTo(p, true, false);
            }
        }
        g.Freeze();
        Dc.DrawGeometry(fill, pen, g);
    }

    public void Text(string s, double x, double y, Brush brush, double size = 0, bool bold = false, bool italic = false, bool underline = false, string align = "left")
    {
        if (string.IsNullOrEmpty(s)) return;
        size = size <= 0 ? Fs : size;
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Geo.Face.FontFamily, italic ? FontStyles.Italic : FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, Geo.Dpi);
        if (underline) ft.SetTextDecorations(TextDecorations.Underline);
        double w = ft.Width;
        double xx = align == "center" ? x - w / 2 : align == "right" ? x - w : x;
        Dc.DrawText(ft, new Point(xx, y));
    }

    public void CenteredWrap(string text, Rect b, double size)
    {
        var lines = Geo.Wrap(text, b.Width - 16, size);
        double y = b.Y + b.Height / 2 - lines.Count * (size + 4) / 2;
        foreach (var l in lines) { Text(l, b.X + b.Width / 2, y, Theme.Ink, size, false, false, false, "center"); y += size + 4; }
    }

    public Brush FillOf(UmlNode n)
    {
        if (!string.IsNullOrEmpty(n.Fill))
        {
            try { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(n.Fill)); b.Freeze(); return b; } catch { }
        }
        return n.Kind == NodeKind.Note ? Theme.NoteFill : Theme.NodeFill;
    }

    public void Shadow(Rect r)
    {
        if (!State.Shadows || (State.Export && Theme.Mode == PaperMode.Blueprint)) return;
        var sh = new SolidColorBrush(Color.FromArgb(26, 10, 16, 30));
        sh.Freeze();
        Dc.DrawRectangle(sh, null, new Rect(r.X + 3, r.Y + 4, r.Width, r.Height));
    }
}