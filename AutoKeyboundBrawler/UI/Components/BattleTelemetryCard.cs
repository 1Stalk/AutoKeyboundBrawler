using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI.Components;

internal static class BattleTelemetryCard
{
    private const float BoxHeight = 90f;
    private const float Padding = 10f;

    public static void Draw(KTGAutomationManager automation)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = BoxHeight * scale;
        var end = origin + new Vector2(width, height);
        var dl = ImGui.GetWindowDrawList();

        // Background surface with top light
        Paint.Surface(dl, origin, end, FF7RTheme.CardRounding * scale, FF7RTheme.Surface0, FF7RTheme.BorderDim, topLight: true);

        // Status resolution
        var status = automation.CurrentStatus;
        var isTyping = automation.IsTyping;
        var isHalt = status.StartsWith("SAFETY") || status.Contains("Halt") || status.Contains("Obstruction");
        var isDuty = automation.CurrentPhase == EventPhase.InMinigame || status.Contains("In Combat");
        var isQueue = status.Contains("Queued") || status.Contains("Commencing") || status.Contains("Registration");

        var statusColor = isHalt ? FF7RTheme.LimitRed :
                          isTyping ? FF7RTheme.MakoCyan :
                          isDuty ? FF7RTheme.MakoCyanSoft :
                          isQueue ? FF7RTheme.AtbGold :
                          FF7RTheme.TextMuted;

        var statusTitle = isHalt ? "TACTICAL PAUSE (SAFETY GUARD)" :
                          isTyping ? "SOLVING PHRASE IN REALTIME" :
                          isDuty ? "IN BATTLE (SCANNING ADDS)" :
                          isQueue ? "DUTY CYCLE IN PROGRESS" :
                          "SYSTEM STANDBY";

        // Top line: Status dot + Title + Database Counter
        var dotCenter = origin + new Vector2(Padding * scale + 4f * scale, Padding * scale + 4f * scale);
        var dotRadius = 3.5f * scale;

        // Animated pulse on active states
        if (isTyping || isDuty || isHalt)
        {
            var pulse = Motion.Pulse(isHalt ? 400.0 : 800.0);
            var pulseCol = FF7RTheme.WithAlpha(statusColor, pulse * 0.5f);
            dl.AddCircleFilled(dotCenter, dotRadius + 3f * scale * pulse, Paint.Col(pulseCol));
        }

        dl.AddCircleFilled(dotCenter, dotRadius, Paint.Col(statusColor));

        var titlePos = new Vector2(dotCenter.X + 10f * scale, origin.Y + Padding * scale);
        dl.AddText(titlePos, Paint.Col(statusColor), statusTitle);

        // Database words loaded on right
        var dbCountText = $"{KTGWordDatabase.Count} WORDS";
        var dbTextSize = ImGui.CalcTextSize(dbCountText);
        var dbPos = new Vector2(end.X - Padding * scale - dbTextSize.X, origin.Y + Padding * scale);
        dl.AddText(dbPos, Paint.Col(FF7RTheme.TextMuted), dbCountText);

        // Word Target Box (Inner framed tactical box with FF7R corner brackets)
        var targetBoxMin = origin + new Vector2(Padding * scale, (Padding + 18f) * scale);
        var targetBoxMax = new Vector2(end.X - Padding * scale, end.Y - (Padding + 14f) * scale);

        Paint.Fill(dl, targetBoxMin, targetBoxMax, FF7RTheme.Surface1, 2f * scale);
        Paint.Stroke(dl, targetBoxMin, targetBoxMax, FF7RTheme.BorderDim, 2f * scale);

        // FF7 Remake tactical corner brackets around target word
        var cornerCol = isTyping ? FF7RTheme.MakoCyan : FF7RTheme.BorderDim;
        Paint.TechCorners(dl, targetBoxMin, targetBoxMax, 6f, cornerCol, 1.5f);

        // Target word text
        var activeWord = automation.ActiveWord;
        var hasWord = !string.IsNullOrEmpty(activeWord);
        var displayWord = hasWord ? $">  \"{activeWord}\"  <" : ">  AWAITING COMBAT WAVE  <";
        var wordColor = isHalt ? FF7RTheme.LimitRedSoft :
                        isTyping ? FF7RTheme.MakoCyanSoft :
                        hasWord ? FF7RTheme.TextStrong :
                        FF7RTheme.TextMuted;

        var wordSize = ImGui.CalcTextSize(displayWord);
        var wordPos = new Vector2(
            targetBoxMin.X + (targetBoxMax.X - targetBoxMin.X - wordSize.X) * 0.5f,
            targetBoxMin.Y + (targetBoxMax.Y - targetBoxMin.Y - wordSize.Y) * 0.5f
        );

        dl.AddText(wordPos, Paint.Col(wordColor), displayWord);

        // Bottom sub-status line
        var subText = status;
        var subSize = ImGui.CalcTextSize(subText);
        var subPos = new Vector2(origin.X + Padding * scale, end.Y - Padding * scale - subSize.Y * 0.8f);
        dl.AddText(subPos, Paint.Col(FF7RTheme.TextSecondary), subText);

        // Advance cursor past the card
        ImGui.SetCursorScreenPos(new Vector2(origin.X, end.Y + 10f * scale));
    }
}

