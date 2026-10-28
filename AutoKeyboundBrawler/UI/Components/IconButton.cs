using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class IconButton
{
    public static bool Draw(FontAwesomeIcon icon, string id, float size, Vector4? color = null, string? tooltip = null, bool enabled = true)
    {
        var origin = ImGui.GetCursorScreenPos();
        var box = new Vector2(size, size);
        var hit = Hit.Area(id, box, enabled);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var center = origin + box * 0.5f;

        if (hover > 0.01f)
        {
            var alpha = hover * (hit.Held ? 0.9f : 0.65f);
            dl.AddRectFilled(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.Surface2, alpha)), 3f);
            dl.AddRect(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.BorderDim, alpha)), 3f);
        }

        var tint = color ?? FF7RTheme.TextSecondary;
        var glyphColor = enabled ? Vector4.Lerp(tint, FF7RTheme.MakoCyan, hover * 0.75f) : FF7RTheme.TextMuted;
        var glyph = icon.ToIconString();
        var glyphSize = ImGui.CalcTextSize(glyph);
        dl.AddText(center - glyphSize * 0.5f, Paint.Col(glyphColor), glyph);

        if (hit.Hovered && !string.IsNullOrEmpty(tooltip))
        {
            ImGui.SetTooltip(tooltip);
        }

        return hit.Clicked;
    }
}

