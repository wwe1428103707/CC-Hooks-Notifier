using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HooksNotifier;

/// <summary>
/// Generates bell icons at runtime using GDI+ (zero external dependencies).
/// Normal: blue (#1296DB). Highlighted: orange (#E8590C) for blinking.
/// </summary>
internal static class IconHelper
{
    private static Icon? _normal;
    private static Icon? _highlighted;

    public static Icon Normal => _normal ??= CreateBell(32, false);
    public static Icon Highlighted => _highlighted ??= CreateBell(32, true);

    /// <summary>Create a bell icon at the given size.</summary>
    public static Icon CreateBell(int size, bool highlighted)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var fillColor = highlighted
            ? Color.FromArgb(0xE8, 0x59, 0x0C) // orange
            : Color.FromArgb(0x12, 0x96, 0xDB); // blue

        float s = size;
        float cx = s / 2;
        float pad = s * 0.08f; // padding from edges

        // ── Bell body ────────────────────────────────────────────────
        using var path = new GraphicsPath();

        // Dimensions (relative to size)
        float domeTop    = pad;
        float domeH      = s * 0.28f;
        float domeW      = s * 0.28f;
        float bodyMid    = s * 0.42f;
        float flareY     = s * 0.70f;
        float flareW     = s * 0.34f;
        float openingY   = s * 0.80f;
        float openingW   = s * 0.12f;

        // Start at top-left of dome
        float leftDomeX  = cx - domeW;
        float rightDomeX = cx + domeW;

        // Dome: arc from left to right
        path.AddArc(cx - domeW, domeTop, domeW * 2, domeH * 2, 180, 180);

        // Right side down to flare
        path.AddLine(rightDomeX, domeTop + domeH, cx + domeW * 0.9f, flareY);

        // Right flare outward
        path.AddLine(cx + domeW * 0.9f, flareY, cx + flareW, openingY);

        // Opening: right side dip
        path.AddLine(cx + flareW, openingY, cx + openingW, openingY);
        path.AddLine(cx + openingW, openingY, cx + openingW, openingY + s * 0.04f);
        path.AddLine(cx + openingW, openingY + s * 0.04f, cx - openingW, openingY + s * 0.04f);

        // Opening: left side up
        path.AddLine(cx - openingW, openingY + s * 0.04f, cx - openingW, openingY);
        path.AddLine(cx - openingW, openingY, cx - flareW, openingY);

        // Left flare inward
        path.AddLine(cx - flareW, openingY, cx - domeW * 0.9f, flareY);

        // Left side up to dome
        path.AddLine(cx - domeW * 0.9f, flareY, leftDomeX, domeTop + domeH);

        path.CloseFigure();

        // Fill bell body
        using var brush = new SolidBrush(fillColor);
        g.FillPath(brush, path);

        // ── Clapper (small circle below opening) ─────────────────────
        float clapSize = s * 0.08f;
        using var clapBrush = new SolidBrush(fillColor);
        g.FillEllipse(clapBrush, cx - clapSize / 2, openingY + s * 0.03f, clapSize, clapSize);

        // ── Highlight / shine (upper-left of dome) ──────────────────
        if (!highlighted)
        {
            using var shinePath = new GraphicsPath();
            shinePath.AddEllipse(cx - domeW * 0.5f, domeTop + domeH * 0.15f,
                                 domeW * 0.6f, domeH * 0.4f);
            using var shineBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
            g.FillPath(shineBrush, shinePath);
        }

        // ── Outline stroke ───────────────────────────────────────────
        using var pen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
        g.DrawPath(pen, path);

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>Release cached icon handles.</summary>
    public static void Cleanup()
    {
        if (_normal != null) { DestroyIcon(_normal.Handle); _normal = null; }
        if (_highlighted != null) { DestroyIcon(_highlighted.Handle); _highlighted = null; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
