using AutoKeyboundBrawler.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class HeaderBar
{
    private const string Title = "AKB";
    private const float PadX = 8f;
    private const float ButtonSize = 18f;
    private const float LangButtonW = 24f;
    private const float ButtonGap = 4f;

    public const float Height = 28f;

    public static bool HandleDrag(Vector2 windowPos, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonsW = (ButtonSize * 2f + LangButtonW + ButtonGap * 2f) * scale;
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
        var isTyping = automation.IsTyping;
        var isHalt = automation.IsHaltStatus;
        var isDuty = automation.IsInDutyPhase;
        var isQueue = automation.IsInQueuePhase;

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
        var rightButtonsLeft = end.X - padX - (ButtonSize * 2f + LangButtonW + ButtonGap * 2f) * scale;
        DrawStatusPill(dl, automation, textLeft, rightButtonsLeft, midY, compact);

        // Window buttons (Compact, Language & Close)
        DrawButtons(window, end, midY, compact);
    }

    private static void DrawStatusPill(ImDrawListPtr dl, KTGAutomationManager automation, float leftX, float rightLimit, float midY, bool compact)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var isTyping = automation.IsTyping;
        var isHalt = automation.IsHaltStatus;
        var isDuty = automation.IsInDutyPhase;
        var isQueue = automation.IsInQueuePhase;

        var badgeText = isHalt ? Loc.Get("BadgeHalt") :
                        isTyping ? Loc.Get("BadgeTyping") :
                        isDuty ? Loc.Get("BadgeCombat") :
                        isQueue ? Loc.Get("BadgeLoop") :
                        Loc.Get("BadgeIdle");

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
        var langW = LangButtonW * scale;
        var gap = ButtonGap * scale;
        var top = midY - buttonSize * 0.5f;

        // 1. Close button (rightmost)
        var closeX = end.X - padX - buttonSize;
        DrawCloseButton(window, new Vector2(closeX, top), buttonSize, scale);

        // 2. Compact toggle button (middle)
        var compactX = closeX - gap - buttonSize;
        DrawCompactButton(window, new Vector2(compactX, top), buttonSize, scale, compact);

        // 3. Language selector button (leftmost of the group)
        var langX = compactX - gap - langW;
        DrawLanguageButton(new Vector2(langX, top), langW, buttonSize, scale);
    }

    private static void DrawCloseButton(MainWindow window, Vector2 origin, float size, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        var box = new Vector2(size, size);
        var hit = Hit.Area("##akb_close", box);
        var hover = Motion.Hover(Motion.Key("##akb_close"), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var center = origin + box * 0.5f;

        // Hover background
        if (hover > 0.01f)
        {
            var alpha = hover * (hit.Held ? 0.45f : 0.25f);
            dl.AddRectFilled(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.LimitRed, alpha)), 3f * scale);
            dl.AddRect(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.LimitRed, hover * 0.65f)), 3f * scale);
        }

        // Geometric Cross (X)
        var crossR = 3.5f * scale;
        var lineCol = Vector4.Lerp(FF7RTheme.TextSecondary, FF7RTheme.LimitRed, hover);
        var thickness = 1.6f * scale;
        dl.AddLine(center - new Vector2(crossR, crossR), center + new Vector2(crossR, crossR), Paint.Col(lineCol), thickness);
        dl.AddLine(center + new Vector2(-crossR, crossR), center + new Vector2(crossR, -crossR), Paint.Col(lineCol), thickness);

        if (hit.Hovered)
        {
            ImGui.SetTooltip(Loc.Get("Close"));
        }

        if (hit.Clicked)
        {
            window.IsOpen = false;
        }
    }

    private static void DrawCompactButton(MainWindow window, Vector2 origin, float size, float scale, bool compact)
    {
        ImGui.SetCursorScreenPos(origin);
        var box = new Vector2(size, size);
        var hit = Hit.Area("##akb_minimize", box);
        var hover = Motion.Hover(Motion.Key("##akb_minimize"), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var center = origin + box * 0.5f;

        // Hover background
        if (hover > 0.01f)
        {
            var alpha = hover * (hit.Held ? 0.9f : 0.65f);
            dl.AddRectFilled(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.Surface2, alpha)), 3f * scale);
            dl.AddRect(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.BorderDim, alpha)), 3f * scale);
        }

        // Geometric Chevron (▲ if expanded to collapse, ▼ if compact to expand)
        var lineCol = Vector4.Lerp(FF7RTheme.TextSecondary, FF7RTheme.MakoCyan, hover * 0.85f);
        var thickness = 1.6f * scale;
        var halfW = 3.8f * scale;
        var halfH = 2.2f * scale;

        if (compact)
        {
            // Pointing Down (expand)
            var p1 = center + new Vector2(-halfW, -halfH);
            var p2 = center + new Vector2(0f, halfH);
            var p3 = center + new Vector2(halfW, -halfH);
            dl.AddLine(p1, p2, Paint.Col(lineCol), thickness);
            dl.AddLine(p2, p3, Paint.Col(lineCol), thickness);
        }
        else
        {
            // Pointing Up (compact)
            var p1 = center + new Vector2(-halfW, halfH);
            var p2 = center + new Vector2(0f, -halfH);
            var p3 = center + new Vector2(halfW, halfH);
            dl.AddLine(p1, p2, Paint.Col(lineCol), thickness);
            dl.AddLine(p2, p3, Paint.Col(lineCol), thickness);
        }

        if (hit.Hovered)
        {
            ImGui.SetTooltip(compact ? Loc.Get("Expand") : Loc.Get("Compact"));
        }

        if (hit.Clicked)
        {
            window.ToggleCompact();
        }
    }

    private static void DrawLanguageButton(Vector2 origin, float width, float height, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        var box = new Vector2(width, height);
        var hit = Hit.Area("##akb_lang", box);
        var hover = Motion.Hover(Motion.Key("##akb_lang"), hit.Hovered);
        var dl = ImGui.GetWindowDrawList();
        var center = origin + box * 0.5f;

        var currentLangCode = Loc.GetLanguageShortName(Loc.CurrentLanguage);

        // Tactical pill badge styling
        var bgAlpha = hover > 0.01f ? (hit.Held ? 0.85f : 0.65f) : 0.35f;
        var borderCol = Vector4.Lerp(FF7RTheme.BorderDim, FF7RTheme.MakoCyan, hover * 0.85f);
        var textCol = Vector4.Lerp(FF7RTheme.TextStrong, FF7RTheme.MakoCyan, hover);

        dl.AddRectFilled(origin, origin + box, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.Surface2, bgAlpha)), 3f * scale);
        dl.AddRect(origin, origin + box, Paint.Col(borderCol), 3f * scale);

        var textSize = ImGui.CalcTextSize(currentLangCode);
        dl.AddText(center - textSize * 0.5f, Paint.Col(textCol), currentLangCode);

        if (hit.Hovered)
        {
            var langTooltip = $"{Loc.Get("LangTooltip")} [{currentLangCode}]";
            ImGui.SetTooltip(langTooltip);
        }

        if (hit.Clicked)
        {
            ImGui.OpenPopup("##akb_lang_popup");
        }

        DrawLanguagePopup();
    }

    private static void DrawLanguagePopup()
    {
        var config = Plugin.Configuration;
        using var popup = ImRaii.Popup("##akb_lang_popup");
        if (!popup) return;

        var selected = config.SelectedLanguage;
        var gameLang = ECommons.DalamudServices.Svc.ClientState.ClientLanguage;
        var autoLabel = $"{Loc.Get("LangAuto")} [{Loc.GetLanguageShortName(gameLang)}]";

        if (ImGui.Selectable(autoLabel, !selected.HasValue))
        {
            config.SelectedLanguage = null;
            config.Save();
        }

        ImGui.Separator();

        if (ImGui.Selectable("English (EN)", selected == Dalamud.Game.ClientLanguage.English))
        {
            config.SelectedLanguage = Dalamud.Game.ClientLanguage.English;
            config.Save();
        }

        if (ImGui.Selectable("日本語 (JA)", selected == Dalamud.Game.ClientLanguage.Japanese))
        {
            config.SelectedLanguage = Dalamud.Game.ClientLanguage.Japanese;
            config.Save();
        }

        if (ImGui.Selectable("Deutsch (DE)", selected == Dalamud.Game.ClientLanguage.German))
        {
            config.SelectedLanguage = Dalamud.Game.ClientLanguage.German;
            config.Save();
        }

        if (ImGui.Selectable("Français (FR)", selected == Dalamud.Game.ClientLanguage.French))
        {
            config.SelectedLanguage = Dalamud.Game.ClientLanguage.French;
            config.Save();
        }
    }
}
