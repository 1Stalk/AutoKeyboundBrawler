using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class FF7ToggleSwitch
{
    private const float TrackWidth = 28f;
    private const float TrackHeight = 14f;
    private const float KnobInset = 2f;

    public static bool Draw(string id, ref bool value, Vector4? activeColor = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(TrackWidth * scale, TrackHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var hit = Hit.Area(id, size);

        var changed = false;
        if (hit.Clicked)
        {
            value = !value;
            changed = true;
        }

        var on = Motion.Approach(Motion.Key(id), value ? 1f : 0f, 18f);
        var hover = Motion.Hover(Motion.Key(id, 1), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var end = origin + size;
        var rounding = 3f * scale;

        var onCol = activeColor ?? FF7RTheme.MakoCyan;
        var offCol = Vector4.Lerp(FF7RTheme.Surface1, FF7RTheme.Surface2, hover);
        var track = Vector4.Lerp(offCol, onCol, on * 0.85f);

        // Mako glow when active
        if (on > 0.05f)
        {
            Paint.Glow(dl, origin, end, rounding, onCol, on * 0.7f);
        }

        // Track surface
        Paint.Fill(dl, origin, end, track, rounding);
        var border = Vector4.Lerp(FF7RTheme.BorderDim, onCol, on);
        Paint.Stroke(dl, origin, end, border, rounding, 1f * scale);

        // Knob travel
        var inset = KnobInset * scale;
        var knobSize = new Vector2(size.Y - inset * 2f, size.Y - inset * 2f);
        var travel = size.X - knobSize.X - inset * 2f;
        var knobPos = new Vector2(origin.X + inset + travel * on, origin.Y + inset);
        var knobEnd = knobPos + knobSize;

        // Knob body
        var knobCol = Vector4.Lerp(FF7RTheme.TextSecondary, FF7RTheme.TextStrong, on);
        Paint.Fill(dl, knobPos, knobEnd, knobCol, 2f * scale);

        // Center pip on knob
        if (on > 0.1f)
        {
            var pipCenter = (knobPos + knobEnd) * 0.5f;
            var pipRadius = 2f * scale;
            dl.AddCircleFilled(pipCenter, pipRadius, Paint.Col(onCol));
        }

        return changed;
    }

    public static bool DrawRow(string id, string title, string description, ref bool value, Vector4? activeColor = null, string? tooltip = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var switchWidth = TrackWidth * scale;

        // Compact: title only, description goes to tooltip
        var titleH = ImGui.GetTextLineHeight();
        var rowH = 22f * scale;
        var boxSize = new Vector2(availWidth, rowH);

        var hit = Hit.Area(id + "_row", boxSize);
        var hover = Motion.Hover(Motion.Key(id + "_row"), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();

        // Subtle row hover background
        if (hover > 0.01f)
        {
            Paint.Fill(dl, origin, origin + boxSize, FF7RTheme.WithAlpha(FF7RTheme.Surface1, hover * 0.5f), 3f * scale);
        }

        var changed = false;
        if (hit.Clicked)
        {
            value = !value;
            changed = true;
        }

        // Draw Title (single line, vertically centered)
        var textLeft = origin.X + 6f * scale;
        var textTop = origin.Y + (rowH - titleH) * 0.5f;

        var titleCol = value ? FF7RTheme.TextStrong : FF7RTheme.TextSecondary;
        dl.AddText(new Vector2(textLeft, textTop), Paint.Col(titleCol), title);

        // Draw Switch aligned to right
        var switchPos = new Vector2(origin.X + availWidth - switchWidth - 6f * scale, origin.Y + (rowH - TrackHeight * scale) * 0.5f);
        ImGui.SetCursorScreenPos(switchPos);
        if (Draw(id, ref value, activeColor))
        {
            changed = true;
        }

        // Advance cursor
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + rowH + 2f * scale));

        // Tooltip: show description (if any) + explicit tooltip
        if (hit.Hovered)
        {
            var tip = "";
            if (!string.IsNullOrEmpty(description)) tip = description;
            if (!string.IsNullOrEmpty(tooltip)) tip = string.IsNullOrEmpty(tip) ? tooltip : $"{tip}\n{tooltip}";
            if (!string.IsNullOrEmpty(tip)) ImGui.SetTooltip(tip);
        }

        return changed;
    }
}

