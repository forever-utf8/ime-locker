namespace ImeLocker.UI;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ImeLocker.Native;
using Microsoft.Win32;

/// <summary>
/// Generates the tray icon dynamically using GDI+, adapting to system theme.
/// Design: "IM" text with a padlock overlay at bottom-right.
/// </summary>
internal static class TrayIconGenerator
{
    private const int IconSize = 32;
    private const int LockSize = 16;

    /// <summary>Detect whether the Windows taskbar uses a dark theme.</summary>
    private static bool IsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Create the tray icon with "IM" text and lock indicator.</summary>
    public static Icon CreateIcon()
    {
        bool dark = IsDarkTheme();
        var foreground = dark ? Color.White : Color.FromArgb(30, 30, 30);
        var accent = dark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(120, 30, 30, 30);

        using var bitmap = new Bitmap(IconSize, IconSize);
        bitmap.SetResolution(96, 96);

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // "IM" text offset toward top-left to leave room for lock
        using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(foreground);
        var textSize = g.MeasureString("IM", font);
        var x = (IconSize - textSize.Width) / 2f - 2f;
        var y = (IconSize - textSize.Height) / 2f - 3f;
        g.DrawString("IM", font, brush, x, y);

        // Lock indicator at bottom-right
        DrawLockIndicator(g, accent, foreground);

        nint hIcon = bitmap.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        var owned = (Icon)temp.Clone();
        User32.DestroyIcon(hIcon);
        return owned;
    }

    private static void DrawLockIndicator(Graphics g, Color accentColor, Color foreground)
    {
        const float ox = IconSize - LockSize;
        const float oy = IconSize - LockSize;
        const float bodyH = 9f;
        const float bodyW = LockSize - 2f;
        const float bodyY = oy + LockSize - bodyH;
        const float bodyX = ox + 1f;
        const float shackleW = 8f;
        const float shackleH = 8f;

        // Shackle arc
        float shackleX = bodyX + (bodyW - shackleW) / 2f;
        float shackleY = bodyY - shackleH / 2f;
        using var pen = new Pen(foreground, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, shackleX, shackleY, shackleW, shackleH, 180, 180);

        // Lock body
        using var bodyBrush = new SolidBrush(accentColor);
        var bodyRect = new RectangleF(bodyX, bodyY, bodyW, bodyH);
        using var bodyPath = RoundedRect(bodyRect, 2f);
        g.FillPath(bodyBrush, bodyPath);

        // Keyhole dot
        float dotSize = 2.5f;
        float dotX = bodyX + (bodyW - dotSize) / 2f;
        float dotY = bodyY + (bodyH - dotSize) / 2f - 0.5f;
        using var keyBrush = new SolidBrush(foreground);
        g.FillEllipse(keyBrush, dotX, dotY, dotSize, dotSize);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
