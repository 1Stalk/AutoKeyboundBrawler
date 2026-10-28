using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class SegmentedTabs
{
    public static int Draw(string id, int selectedIndex, string[] items)
    {
        if (items.Length == 0) return selectedIndex;

        var scale = ImGuiHelpers.GlobalScale;
        var availWidth = ImGui.GetContentRegionAvail().X;
        var tabHeight = 28f * scale;
        var tabWidth = availWidth / items.Length;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        // Background strip
        Paint.Fill(dl, origin, origin + new Vector2(availWidth, tabHeight), FF7RTheme.Surface0, 3f * scale);
        Paint.Stroke(dl, origin, origin + new Vector2(availWidth, tabHeight), FF7RTheme.BorderDim, 3f * scale);

        for (var i = 0; i < items.Length; i++)
        {
            var tabMin = origin + new Vector2(i * tabWidth, 0);
            var tabMax = tabMin + new Vector2(tabWidth, tabHeight);
            var tabId = $"{id}_tab_{i}";

            ImGui.SetCursorScreenPos(tabMin);
            var hit = Hit.Area(tabId, new Vector2(tabWidth, tabHeight));
            var isSelected = (i == selectedIndex);

            if (hit.Clicked)
            {
                selectedIndex = i;
                isSelected = true;
            }

            var hover = Motion.Hover(Motion.Key(tabId), hit.Hovered);

            // Tab hover/active background
            if (isSelected)
            {
                Paint.Fill(dl, tabMin, tabMax, FF7RTheme.WithAlpha(FF7RTheme.MakoCyan, 0.12f), 3f * scale);

                // Active bottom neon bar
                var barHeight = 2.5f * scale;
                dl.AddRectFilled(new Vector2(tabMin.X + 4f * scale, tabMax.Y - barHeight),
                    new Vector2(tabMax.X - 4f * scale, tabMax.Y),
                    Paint.Col(FF7RTheme.MakoCyan), 1f);
            }
            else if (hover > 0.01f)
            {
                Paint.Fill(dl, tabMin, tabMax, FF7RTheme.WithAlpha(FF7RTheme.Surface2, hover * 0.4f), 3f * scale);
            }

            // Divider between tabs
            if (i > 0)
            {
                dl.AddLine(new Vector2(tabMin.X, tabMin.Y + 4f * scale), new Vector2(tabMin.X, tabMax.Y - 4f * scale), Paint.Col(FF7RTheme.Hairline));
            }

            // Label
            var label = items[i];
            var textSize = ImGui.CalcTextSize(label);
            var textPos = tabMin + (new Vector2(tabWidth, tabHeight) - textSize) * 0.5f;
            var textCol = isSelected ? FF7RTheme.MakoCyan : (hit.Hovered ? FF7RTheme.TextStrong : FF7RTheme.TextSecondary);
            dl.AddText(textPos, Paint.Col(textCol), label);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + tabHeight + 10f * scale));
        return selectedIndex;
    }
}

