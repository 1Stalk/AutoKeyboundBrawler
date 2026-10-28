using AutoKeyboundBrawler.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class HeaderBar
{
    private const string Title = "AUTO KEYBOUND BRAWLER";
    private const string Subtitle = "TACTICAL SOLVER";
    private const float PadX = 12f;
    private const float IconBox = 26f;
    private const float ButtonSize = 24f;
    private const float ButtonGap = 5f;

    public const float Height = 44f;

    public static bool HandleDrag(Vector2 windowPos, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonsW = (ButtonSize * 2f + ButtonGap) * scale;
        var dragWidth = width - PadX * scale - buttonsW - 8f * scale;

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

        // Tech divider underneath header if not compact
        if (!compact)
        {
            Paint.TechDivider(dl, new Vector2(origin.X, end.Y - 1f), width, FF7RTheme.MakoCyan);
        }

        // Draw App Icon
        var iconBox = IconBox * scale;
        var iconMin = new Vector2(origin.X + padX, midY - iconBox * 0.5f);
        var iconMax = iconMin + new Vector2(iconBox, iconBox);

        // Subtle glow around icon
        Paint.Glow(dl, iconMin, iconMax, 4f * scale, FF7RTheme.MakoCyan, 0.45f);
        AppIcon.Draw(dl, iconMin, iconMax, 4f * scale);
        Paint.Stroke(dl, iconMin, iconMax, FF7RTheme.MakoCyan, 4f * scale, 1f * scale);

        // Titles
        var textLeft = iconMax.X + 10f * scale;
        var titleSize = ImGui.CalcTextSize(Title);

        if (compact)
        {
            // In compact mode: single line title or status
            dl.AddText(new Vector2(textLeft, midY - titleSize.Y * 0.5f), Paint.Col(FF7RTheme.TextStrong), Title);
            textLeft += titleSize.X + 12f * scale;
        }
        else
        {
            // Expanded mode: Title + Subtitle
            var titleTop = midY - 14f * scale;
            dl.AddText(new Vector2(textLeft, titleTop), Paint.Col(FF7RTheme.TextStrong), Title);
            dl.AddText(new Vector2(textLeft, titleTop + 14f * scale), Paint.Col(FF7RTheme.MakoCyan), Subtitle);
            textLeft += titleSize.X + 14f * scale;
        }

        // Status pill
        var rightButtonsLeft = end.X - padX - (ButtonSize * 2f + ButtonGap) * scale;
        DrawStatusPill(dl, automation, textLeft, rightButtonsLeft, midY, compact);

        // Window buttons (Minimize/Compact & Close)
        DrawButtons(window, end, midY, compact);
    }

    private static void DrawStatusPill(ImDrawListPtr dl, KTGAutomationManager automation, float leftX, float rightLimit, float midY, bool compact)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var status = automation.CurrentStatus;
        var isTyping = automation.IsTyping;
        var isHalt = status.StartsWith("SAFETY") || status.Contains("Halt") || status.Contains("Obstruction");
        var isDuty = automation.CurrentPhase == EventPhase.InMinigame || status.Contains("In Combat");
        var isQueue = status.Contains("Queued") || status.Contains("Commencing") || status.Contains("Registration");

        var badgeText = isHalt ? "HALTED" :
                        isTyping ? "TYPING" :
                        isDuty ? "COMBAT" :
                        isQueue ? "AFK LOOP" :
                        "IDLE";

        var badgeColor = isHalt ? FF7RTheme.LimitRed :
                         isTyping ? FF7RTheme.MakoCyan :
                         isDuty ? FF7RTheme.MakoCyanSoft :
                         isQueue ? FF7RTheme.AtbGold :
                         FF7RTheme.TextMuted;

        var textSize = ImGui.CalcTextSize(badgeText);
        var pillW = textSize.X + 16f * scale;
        var pillH = 18f * scale;

        // Position pill before window control buttons
        var pillRight = rightLimit - 8f * scale;
        var pillLeft = pillRight - pillW;

        if (pillLeft < leftX) return; // Don't overlap title

        var pillMin = new Vector2(pillLeft, midY - pillH * 0.5f);
        var pillMax = new Vector2(pillRight, midY + pillH * 0.5f);

        // Draw tactical badge
        Paint.Fill(dl, pillMin, pillMax, FF7RTheme.WithAlpha(badgeColor, 0.15f), 3f * scale);
        Paint.Stroke(dl, pillMin, pillMax, FF7RTheme.WithAlpha(badgeColor, 0.70f), 3f * scale);

        var textPos = pillMin + (new Vector2(pillW, pillH) - textSize) * 0.5f;
        dl.AddText(textPos, Paint.Col(badgeColor), badgeText);

        // If in compact mode and typing, also show the active word right next to the badge!
        if (compact && isTyping && !string.IsNullOrEmpty(automation.ActiveWord))
        {
            var wordText = $"\"{automation.ActiveWord}\"";
            var wordSize = ImGui.CalcTextSize(wordText);
            var wordRight = pillMin.X - 8f * scale;
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
        if (IconButton.Draw(FontAwesomeIcon.Times, "##akb_close", buttonSize, tooltip: "Close Window"))
        {
            window.IsOpen = false;
        }

        // Compact toggle button (Chevron)
        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonSize - stride, top));
        var icon = compact ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp;
        if (IconButton.Draw(icon, "##akb_minimize", buttonSize, tooltip: compact ? "Expand Window" : "Compact HUD Mode"))
        {
            window.ToggleCompact();
        }
    }
}

