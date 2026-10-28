using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI;

internal static class Paint
{
    private const int ShadowLayers = 4;

    public static uint Col(Vector4 color) => ImGui.GetColorU32(color);

    private static uint Opaque(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color with { W = 1f });

    public static void Fill(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float rounding, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
        => dl.AddRectFilled(min, max, Col(color), rounding, flags);

    public static void Stroke(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float rounding, float thickness = 1f, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
        => dl.AddRect(min, max, Col(color), rounding, flags, thickness);

    public static void Gradient(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 top, Vector4 bottom, float rounding, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
    {
        var start = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, Col(new Vector4(1f, 1f, 1f, top.W)), rounding, flags);
        var end = dl.VtxBuffer.Size;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, start, end, min, new Vector2(min.X, max.Y), Opaque(top), Opaque(bottom));
    }

    public static void GradientH(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 left, Vector4 right, float rounding, ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
    {
        var start = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, Col(new Vector4(1f, 1f, 1f, left.W)), rounding, flags);
        var end = dl.VtxBuffer.Size;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, start, end, min, new Vector2(max.X, min.Y), Opaque(left), Opaque(right));
    }

    public static void Glow(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 color, float intensity)
    {
        var scale = ImGuiHelpers.GlobalScale;
        for (var layer = 3; layer >= 1; layer--)
        {
            var grow = new Vector2(layer * 2.5f * scale, layer * 2.5f * scale);
            var alpha = 0.05f * (4 - layer) * intensity;
            dl.AddRectFilled(min - grow, max + grow, Col(FF7RTheme.WithAlpha(color, alpha)), rounding + grow.X);
        }
    }

    public static void Shadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float spread, float alpha)
    {
        var offset = new Vector2(0f, spread * 0.35f);
        for (var layer = ShadowLayers; layer >= 1; layer--)
        {
            var t = layer / (float)ShadowLayers;
            var grow = new Vector2(spread * t, spread * t);
            var layerAlpha = alpha / ShadowLayers * (1.2f - t);
            dl.AddRectFilled(min - grow + offset, max + grow + offset, Col(new Vector4(0f, 0f, 0f, layerAlpha)), rounding + grow.X);
        }
    }

    public static void TopLight(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float alpha = 0.10f)
        => dl.AddLine(new Vector2(min.X + rounding, min.Y + 1f), new Vector2(max.X - rounding, min.Y + 1f), Col(new Vector4(1f, 1f, 1f, alpha)), 1f);

    public static void Surface(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 fill, Vector4 border, bool topLight = true, float borderThickness = 1f)
    {
        Fill(dl, min, max, fill, rounding);
        if (topLight) TopLight(dl, min, max, rounding);
        Stroke(dl, min, max, border, rounding, borderThickness);
    }

    public static void Pill(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 fill, Vector4 border)
    {
        var rounding = (max.Y - min.Y) * 0.5f;
        Fill(dl, min, max, fill, rounding);
        Stroke(dl, min, max, border, rounding);
    }

    // FF7 Remake Tactical HUD Elements: Sharp corner brackets
    public static void TechCorners(ImDrawListPtr dl, Vector2 min, Vector2 max, float len, Vector4 color, float thickness = 1.5f)
    {
        var col = Col(color);
        var scale = ImGuiHelpers.GlobalScale;
        var l = len * scale;
        var t = thickness * scale;

        // Top-left ┌
        dl.AddLine(min, new Vector2(min.X + l, min.Y), col, t);
        dl.AddLine(min, new Vector2(min.X, min.Y + l), col, t);

        // Top-right ┐
        dl.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X - l, min.Y), col, t);
        dl.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X, min.Y + l), col, t);

        // Bottom-left └
        dl.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X + l, max.Y), col, t);
        dl.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X, max.Y - l), col, t);

        // Bottom-right ┘
        dl.AddLine(max, new Vector2(max.X - l, max.Y), col, t);
        dl.AddLine(max, new Vector2(max.X, max.Y - l), col, t);
    }

    // FF7 Remake Tactical Divider with glowing diamond pip
    public static void TechDivider(ImDrawListPtr dl, Vector2 start, float width, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var end = new Vector2(start.X + width, start.Y);

        // Subtle line
        dl.AddLine(start, end, Col(FF7RTheme.Hairline), 1f);

        // Diamond pip on the left
        var diamondSize = 3f * scale;
        var diamondCenter = new Vector2(start.X + diamondSize + 2f * scale, start.Y);
        dl.AddQuadFilled(
            diamondCenter + new Vector2(0, -diamondSize),
            diamondCenter + new Vector2(diamondSize, 0),
            diamondCenter + new Vector2(0, diamondSize),
            diamondCenter + new Vector2(-diamondSize, 0),
            Col(color));
    }
}

