using AutoKeyboundBrawler.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class HeaderBar
{
    private const string Title = "AKB";
    private const float PadX = 8f;
    private const float ButtonSize = 18f;
    private const float ButtonGap = 3f;

    public const float Height = 28f;

    public static bool HandleDrag(Vector2 windowPos, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonsW = (ButtonSize * 2f + ButtonGap) * scale;
        var dragWidth = width - PadX * scale - buttonsW - 6f * scale;

        ImGui.SetCursorScreenPos(windowPos);
        ImGui.InvisibleButton("##akb_drag", new Vector2(MathF.Max(1f, dragWidth), height));

        var doubleClicked = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
        if (!ImGui.IsItemActive()) return doubleClicked;

        var delta = ImGui.GetIO().MouseDelta;
        if (delta != Vector2.Zero)
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + delta, ImGuiCond.Always);
        }

        return doubleClicked;
    }

    public static void Draw(MainWindow window, KTGAutomationManager automation, Vector2 origin, float width, float height, bool compact)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var end = origin + new Vector2(width, height);
        var padX = PadX * scale;
        var midY = origin.Y + height * 0.5f;

        // Header background fill
        Paint.Fill(dl, origin, end, FF7RTheme.Surface0, FF7RTheme.WindowRounding * scale,
            compact ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersTop);

        // Subtle bottom separator if not compact
        if (!compact)
        {
            dl.AddLine(new Vector2(origin.X, end.Y - 1f), new Vector2(end.X, end.Y - 1f), Paint.Col(FF7RTheme.Hairline), 1f);
        }

        // Title text
        var titleSize = ImGui.CalcTextSize(Title);
        var titlePos = new Vector2(origin.X + padX, midY - titleSize.Y * 0.5f);
        dl.AddText(titlePos, Paint.Col(FF7RTheme.TextStrong), Title);
        var textLeft = titlePos.X + titleSize.X + 8f * scale;

        // Status dot
        var dotRadius = 3f * scale;
        var dotCenter = new Vector2(textLeft + dotRadius, midY);
        var status = automation.CurrentStatus;
        var isTyping = automation.IsTyping;
        var isHalt = status.StartsWith("SAFETY") || status.Contains("Halt") || status.Contains("Obstruction");
        var isDuty = automation.CurrentPhase == EventPhase.InMinigame || status.Contains("In Combat");
        var isQueue = automation.CurrentPhase is EventPhase.InQueue or EventPhase.Commencing or EventPhase.Registering || status.Contains("Queued") || status.Contains("Commencing") || status.Contains("Registration");

        var dotColor = isHalt ? FF7RTheme.LimitRed :
                       isTyping ? FF7RTheme.MakoCyan :
                       isDuty ? FF7RTheme.MakoCyanSoft :
                       isQueue ? FF7RTheme.AtbGold :
                       FF7RTheme.TextMuted;

        // Pulse on active states
        if (isTyping || isDuty || isHalt)
        {
            var pulse = Motion.Pulse(isHalt ? 400.0 : 800.0);
            dl.AddCircleFilled(dotCenter, dotRadius + 2f * scale * pulse, Paint.Col(FF7RTheme.WithAlpha(dotColor, pulse * 0.4f)));
        }
        dl.AddCircleFilled(dotCenter, dotRadius, Paint.Col(dotColor));
        textLeft = dotCenter.X + dotRadius + 6f * scale;

        // Status pill
        var rightButtonsLeft = end.X - padX - (ButtonSize * 2f + ButtonGap) * scale;
        DrawStatusPill(dl, automation, textLeft, rightButtonsLeft, midY, compact);

        // Window buttons (Compact & Close)
        DrawButtons(window, end, midY, compact);
    }

    private static void DrawStatusPill(ImDrawListPtr dl, KTGAutomationManager automation, float leftX, float rightLimit, float midY, bool compact)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var status = automation.CurrentStatus;
        var isTyping = automation.IsTyping;
        var isHalt = status.StartsWith("SAFETY") || status.Contains("Halt") || status.Contains("Obstruction");
        var isDuty = automation.CurrentPhase == EventPhase.InMinigame || status.Contains("In Combat");
        var isQueue = automation.CurrentPhase is EventPhase.InQueue or EventPhase.Commencing or EventPhase.Registering || status.Contains("Queued") || status.Contains("Commencing") || status.Contains("Registration");

        var badgeText = isHalt ? "HALTED" :
                        isTyping ? "TYPING" :
                        isDuty ? "COMBAT" :
                        isQueue ? "LOOP" :
                        "IDLE";

        var badgeColor = isHalt ? FF7RTheme.LimitRed :
                         isTyping ? FF7RTheme.MakoCyan :
                         isDuty ? FF7RTheme.MakoCyanSoft :
                         isQueue ? FF7RTheme.AtbGold :
                         FF7RTheme.TextMuted;

        var textSize = ImGui.CalcTextSize(badgeText);
        var pillW = textSize.X + 12f * scale;
        var pillH = 16f * scale;

        // Position pill before window control buttons
        var pillRight = rightLimit - 4f * scale;
        var pillLeft = pillRight - pillW;

        if (pillLeft < leftX) return; // Don't overlap title

        var pillMin = new Vector2(pillLeft, midY - pillH * 0.5f);
        var pillMax = new Vector2(pillRight, midY + pillH * 0.5f);

        // Draw compact badge
        Paint.Fill(dl, pillMin, pillMax, FF7RTheme.WithAlpha(badgeColor, 0.12f), 3f * scale);
        Paint.Stroke(dl, pillMin, pillMax, FF7RTheme.WithAlpha(badgeColor, 0.55f), 3f * scale);

        var textPos = pillMin + (new Vector2(pillW, pillH) - textSize) * 0.5f;
        dl.AddText(textPos, Paint.Col(badgeColor), badgeText);

        // If in compact mode and typing, also show the active word
        if (compact && isTyping && !string.IsNullOrEmpty(automation.ActiveWord))
        {
            var wordText = $"\"{automation.ActiveWord}\"";
            var wordSize = ImGui.CalcTextSize(wordText);
            var wordRight = pillMin.X - 6f * scale;
            var wordLeft = wordRight - wordSize.X;
            if (wordLeft > leftX)
            {
                dl.AddText(new Vector2(wordLeft, midY - wordSize.Y * 0.5f), Paint.Col(FF7RTheme.MakoCyan), wordText);
            }
        }
    }

    private static void DrawButtons(MainWindow window, Vector2 end, float midY, bool compact)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = PadX * scale;
        var buttonSize = ButtonSize * scale;
        var stride = buttonSize + ButtonGap * scale;
        var top = midY - buttonSize * 0.5f;

        // Close button (X)
        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonSize, top));
        if (IconButton.Draw(FontAwesomeIcon.Times, "##akb_close", buttonSize, tooltip: "Close"))
        {
            window.IsOpen = false;
        }

        // Compact toggle button (Chevron)
        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonSize - stride, top));
        var icon = compact ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp;
        if (IconButton.Draw(icon, "##akb_minimize", buttonSize, tooltip: compact ? "Expand" : "Compact"))
        {
            window.ToggleCompact();
        }
    }
}

