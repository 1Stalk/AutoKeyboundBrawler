using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class TacticalSlider
{
    private const float HitHeight = 12f;
    private const float TrackHeight = 2.5f;

    public static bool Draw(string id, string label, ref int value, int min, int max, string unit = "ms")
    {
        var scale = ImGuiHelpers.GlobalScale;
        var availWidth = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        // 1. Label and Value row (clean, borderless typography)
        var labelCol = FF7RTheme.TextSecondary;
        dl.AddText(origin, Paint.Col(labelCol), label);

        var valueText = $"{value} {unit}";
        var valSize = ImGui.CalcTextSize(valueText);
        var valPos = new Vector2(origin.X + availWidth - valSize.X, origin.Y);
        dl.AddText(valPos, Paint.Col(FF7RTheme.MakoCyan), valueText);

        var textHeight = ImGui.GetTextLineHeight();
        var sliderY = origin.Y + textHeight + 4f * scale;

        // 2. Invisible button for mouse hit testing & dragging
        var hitStart = new Vector2(origin.X, sliderY);
        var hitSize = new Vector2(availWidth, HitHeight * scale);
        ImGui.SetCursorScreenPos(hitStart);

        var hit = Hit.Area(id, hitSize);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var changed = false;

        if (hit.Held)
        {
            var mouseX = ImGui.GetIO().MousePos.X;
            var fraction = Math.Clamp((mouseX - origin.X) / availWidth, 0f, 1f);
            var newValue = Math.Clamp((int)MathF.Round(min + fraction * (max - min)), min, max);
            if (newValue != value)
            {
                value = newValue;
                changed = true;
            }
        }

        // 3. Render Sleek Minimalist Track
        var trackY = sliderY + (HitHeight * scale - TrackHeight * scale) * 0.5f;
        var trackStart = new Vector2(origin.X, trackY);
        var trackEnd = new Vector2(origin.X + availWidth, trackY + TrackHeight * scale);
        var trackRounding = TrackHeight * scale * 0.5f;

        // Inactive background track
        Paint.Fill(dl, trackStart, trackEnd, FF7RTheme.Surface2, trackRounding);

        // Active filled progress
        var currentFraction = Math.Clamp((float)(value - min) / (max - min), 0f, 1f);
        var knobCenterX = origin.X + currentFraction * availWidth;
        var knobCenterY = trackY + (TrackHeight * scale) * 0.5f;

        if (currentFraction > 0.005f)
        {
            var activeEnd = new Vector2(knobCenterX, trackEnd.Y);
            Paint.Fill(dl, trackStart, activeEnd, FF7RTheme.MakoCyan, trackRounding);
        }

        // 4. Sleek Thumb / Knob
        var isEngaged = hit.Held || hit.Hovered;
        var knobRadius = (hit.Held ? 4.5f : (hit.Hovered ? 4.0f : 3.0f)) * scale;

        if (hover > 0.05f || hit.Held)
        {
            var glowAlpha = hit.Held ? 0.55f : hover * 0.35f;
            dl.AddCircleFilled(new Vector2(knobCenterX, knobCenterY), knobRadius + 3f * scale,
                Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.MakoCyan, glowAlpha)));
        }

        dl.AddCircleFilled(new Vector2(knobCenterX, knobCenterY), knobRadius, Paint.Col(FF7RTheme.TextStrong));
        dl.AddCircle(new Vector2(knobCenterX, knobCenterY), knobRadius, Paint.Col(FF7RTheme.MakoCyan), 0, 1.4f * scale);

        // Advance cursor
        var totalHeight = textHeight + 3f * scale + HitHeight * scale + 2f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + totalHeight));

        return changed;
    }
}
