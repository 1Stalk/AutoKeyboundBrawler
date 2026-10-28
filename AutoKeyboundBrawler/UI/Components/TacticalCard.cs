using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal sealed class TacticalCard : IDisposable
{
    private const float PaddingX = 10f;
    private const float PaddingY = 8f;
    private const float BottomGap = 10f;

    private readonly Vector2 _origin;
    private readonly float _width;
    private readonly string? _tag;

    public static TacticalCard Begin(string? sectionTag = null)
    {
        return new TacticalCard(sectionTag);
    }

    private TacticalCard(string? tag)
    {
        var scale = ImGuiHelpers.GlobalScale;
        _tag = tag;
        _origin = ImGui.GetCursorScreenPos();
        _width = ImGui.GetContentRegionAvail().X;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var topOffset = (tag != null ? 22f : PaddingY) * scale;
        ImGui.SetCursorScreenPos(_origin + new Vector2(PaddingX * scale, topOffset));
        ImGui.BeginGroup();
    }

    public void Dispose()
    {
        ImGui.EndGroup();
        var scale = ImGuiHelpers.GlobalScale;
        var bottomPadding = PaddingY * scale;
        var contentEnd = ImGui.GetItemRectMax();
        var cardEnd = new Vector2(_origin.X + _width, contentEnd.Y + bottomPadding);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSetCurrent(0);

        // Draw card background
        var rounding = FF7RTheme.CardRounding * scale;
        Paint.Surface(dl, _origin, cardEnd, rounding, FF7RTheme.Surface0, FF7RTheme.BorderDim, topLight: true);

        // Draw Section Tag if provided (e.g. "// 01 COMBAT AUTOMATION")
        if (!string.IsNullOrEmpty(_tag))
        {
            var tagPos = _origin + new Vector2(10f * scale, 6f * scale);
            dl.AddText(tagPos, Paint.Col(FF7RTheme.MakoCyan), _tag);

            // Tech line next to tag
            var tagSize = ImGui.CalcTextSize(_tag);
            var lineStart = new Vector2(tagPos.X + tagSize.X + 8f * scale, tagPos.Y + tagSize.Y * 0.5f);
            var lineEnd = new Vector2(_origin.X + _width - 10f * scale, lineStart.Y);
            if (lineEnd.X > lineStart.X)
            {
                dl.AddLine(lineStart, lineEnd, Paint.Col(FF7RTheme.Hairline), 1f);
            }
        }

        dl.ChannelsMerge();

        // Advance cursor
        ImGui.SetCursorScreenPos(new Vector2(_origin.X, cardEnd.Y + BottomGap * scale));
    }
}

