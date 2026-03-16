namespace ImeLocker.UI;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

/// <summary>
/// Generates tray icons dynamically using GDI+, adapting to system theme.
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
            // SystemUsesLightTheme: 0 = dark, 1 = light
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return true; // default to dark theme
        }
    }

    /// <summary>Create the idle/default icon with "IM" text and a lock indicator.</summary>
    public static Icon CreateIdleIcon()
    {
        return CreateTextIcon("IM", null);
    }

    /// <summary>Create icon for English input mode.</summary>
    public static Icon CreateEnglishIcon()
    {
        return CreateTextIcon("A", CreateAccentColor(isEnglish: true));
    }

    /// <summary>Create icon for Chinese input mode.</summary>
    public static Icon CreateChineseIcon()
    {
        return CreateTextIcon("中", CreateAccentColor(isEnglish: false));
    }

    private static Color CreateAccentColor(bool isEnglish)
    {
        // English: blue accent, Chinese: orange accent
        return isEnglish
            ? Color.FromArgb(180, 80, 160, 255)
            : Color.FromArgb(180, 255, 140, 50);
    }

    private static Icon CreateTextIcon(string text, Color? accentColor)
    {
        bool dark = IsDarkTheme();
        var foreground = dark ? Color.White : Color.FromArgb(30, 30, 30);
        var accent = accentColor ?? (dark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(120, 30, 30, 30));

        using var bitmap = new Bitmap(IconSize, IconSize);
        bitmap.SetResolution(96, 96);

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // Draw main text — large, positioned top-left to allow lock overlay at bottom-right
        var fontSize = text == "IM" ? 15f : 28f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(foreground);

        var textSize = g.MeasureString(text, font);
        // Offset toward top-left so text and lock overlap naturally
        var x = (IconSize - textSize.Width) / 2f - 2f;
        var y = (IconSize - textSize.Height) / 2f - 3f;
        g.DrawString(text, font, brush, x, y);

        // Draw lock indicator at bottom-right, overlapping the text
        DrawLockIndicator(g, accent, foreground);

        // Convert bitmap to icon
        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>Draw a padlock at the bottom-right, rendered on top of text.</summary>
    private static void DrawLockIndicator(Graphics g, Color accentColor, Color foreground)
    {
        const float ox = IconSize - LockSize;     // x origin
        const float oy = IconSize - LockSize;     // y origin
        const float bodyH = 9f;
        const float bodyW = LockSize - 2f;
        const float bodyY = oy + LockSize - bodyH;
        const float bodyX = ox + 1f;
        const float shackleW = 8f;
        const float shackleH = 8f;

        // Clear background behind the lock so it stands out over text
        // Use a slightly larger region for clean overlap
        using var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));

        // Shackle (U-shape arc)
        float shackleX = bodyX + (bodyW - shackleW) / 2f;
        float shackleY = bodyY - shackleH / 2f;
        using var pen = new Pen(foreground, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, shackleX, shackleY, shackleW, shackleH, 180, 180);

        // Lock body (filled rounded rect)
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
