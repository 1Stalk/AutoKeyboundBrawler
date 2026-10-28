using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI;

public class MainWindow : Window, IDisposable
{
    private const float CollapseMs = 240f;
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;

    private static readonly Vector2 DefaultSize = new(280, 260);
    private static readonly WindowSizeConstraints CompactConstraints = new()
    {
        MinimumSize = new Vector2(240, HeaderBar.Height),
        MaximumSize = new Vector2(float.MaxValue, HeaderBar.Height),
    };
    private static readonly WindowSizeConstraints ExpandedConstraints = new()
    {
        MinimumSize = new Vector2(260, 220),
        MaximumSize = new Vector2(360, 420),
    };

    private readonly Configuration _config;
    private readonly KTGAutomationManager _automation;

    private bool _compact;
    private float _collapse;
    private float _collapseFrom;
    private long _collapseTick;
    private bool _sizeDriven;
    private Vector2 _expandedSize = DefaultSize;
    private IDisposable? _chrome;

    public MainWindow(Configuration config, KTGAutomationManager automation)
        : base("AutoKeyboundBrawler###AutoKeyboundBrawlerMainWindow", BaseFlags)
    {
        _config = config;
        _automation = automation;

        Size = DefaultSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = ExpandedConstraints;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public void Dispose()
    {
    }

    public void ToggleCompact()
    {
        _compact = !_compact;
        _collapseFrom = _collapse;
        _collapseTick = Environment.TickCount64;
    }

    public override void PreDraw()
    {
        _chrome = FF7RTheme.PushChrome(Vector2.Zero);
        AdvanceCollapse();

        var wasSizeDriven = _sizeDriven;
        _sizeDriven = _compact || _collapse > 0f;
        Flags = _sizeDriven ? BaseFlags | ImGuiWindowFlags.NoResize : BaseFlags;
        SizeConstraints = _sizeDriven ? CompactConstraints : ExpandedConstraints;

        var scale = ImGuiHelpers.GlobalScale;
        if (_sizeDriven)
        {
            var headerH = HeaderBar.Height * scale;
            var height = _expandedSize.Y + (headerH - _expandedSize.Y) * _collapse;
            Size = new Vector2(_expandedSize.X, height);
            SizeCondition = ImGuiCond.Always;
            return;
        }

        if (wasSizeDriven)
        {
            Size = _expandedSize;
            SizeCondition = ImGuiCond.Always;
            return;
        }

        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private void AdvanceCollapse()
    {
        var target = _compact ? 1f : 0f;
        var span = MathF.Abs(target - _collapseFrom);
        if (span <= 0.0001f)
        {
            _collapse = target;
            return;
        }

        var elapsed = (Environment.TickCount64 - _collapseTick) / (CollapseMs * span);
        var t = Math.Clamp(elapsed, 0f, 1f);
        _collapse = _collapseFrom + (target - _collapseFrom) * Motion.EaseInOutCubic(t);
    }

    public override void PostDraw()
    {
        _chrome?.Dispose();
        _chrome = null;
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var headerHeight = HeaderBar.Height * scale;
        var windowSize = ImGui.GetWindowSize();
        var windowPos = ImGui.GetWindowPos();

        // Drag handling on header
        if (HeaderBar.HandleDrag(windowPos, windowSize.X, headerHeight))
        {
            ToggleCompact();
        }

        if (!_sizeDriven)
        {
            _expandedSize = windowSize;
        }

        // In compact mode
        if (_collapse >= 0.999f)
        {
            HeaderBar.Draw(this, _automation, windowPos, windowSize.X, headerHeight, compact: true);
            return;
        }

        // Draw Header
        HeaderBar.Draw(this, _automation, windowPos, windowSize.X, headerHeight, compact: false);

        // Body Content
        var bodyTop = windowPos.Y + headerHeight + 4f * scale;
        var bodyPaddingX = 8f * scale;
        var contentWidth = windowSize.X - bodyPaddingX * 2f;
        var bodyHeight = windowSize.Y - headerHeight - 8f * scale;

        if (bodyHeight < 10f) return;

        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + bodyPaddingX, bodyTop));

        using (Motion.PushAlpha(MathF.Max(0.001f, 1f - _collapse)))
        using (var bodyChild = ImRaii.Child("##akb_body", new Vector2(contentWidth, bodyHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (!bodyChild) return;

            var dl = ImGui.GetWindowDrawList();

            // 1. Active Word Bar (compact replacement for BattleTelemetryCard)
            DrawWordBar(dl, contentWidth, scale);

            // 2. Main Toggles
            var minigame = _config.MinigameAutoplay;
            if (FF7ToggleSwitch.DrawRow("##minigame_solver", "Typing Solver", "Humanized typing, shield detection & adds priority", ref minigame,
                FF7RTheme.MakoCyan, "Automatically solves enemy phrases letter-by-letter with humanized typing delays."))
            {
                _config.SetMinigameAutoplay(minigame);
            }

            var autoLoop = _config.AutoLoopEnabled;
            if (FF7ToggleSwitch.DrawRow("##afk_loop", "AFK Loop", "Auto-register, commence & exit results", ref autoLoop,
                FF7RTheme.AtbGold, "Automatically interacts with Registrar, confirms entry, commences duty, and exits results."))
            {
                _config.SetAutoLoop(autoLoop);
            }

            // 3. Section: SPEED
            DrawSectionLabel(dl, "SPEED", contentWidth, scale);

            var minDelay = _config.CharDelayMinMs;
            if (TacticalSlider.Draw("char_min", "Min Keystroke Delay", ref minDelay, 10, 150))
            {
                _config.CharDelayMinMs = minDelay;
                if (_config.CharDelayMaxMs < minDelay) _config.CharDelayMaxMs = minDelay;
                _config.Save();
            }

            var maxDelay = _config.CharDelayMaxMs;
            if (TacticalSlider.Draw("char_max", "Max Keystroke Delay", ref maxDelay, 15, 250))
            {
                _config.CharDelayMaxMs = Math.Max(_config.CharDelayMinMs, maxDelay);
                _config.Save();
            }

            var enterDelay = _config.SubmissionDelayMs;
            if (TacticalSlider.Draw("sub_delay", "Submit Delay", ref enterDelay, 20, 300))
            {
                _config.SubmissionDelayMs = enterDelay;
                _config.Save();
            }

            var ignorePunct = _config.IgnorePunctuation;
            if (FF7ToggleSwitch.DrawRow("##ignore_punct", "Ignore Punctuation", "Auto-strip !, ?, ', - from phrases", ref ignorePunct,
                FF7RTheme.MakoCyan, "Automatically strips punctuation symbols to guarantee faster, error-free typing."))
            {
                _config.IgnorePunctuation = ignorePunct;
                _config.Save();
            }
        }
    }

    private void DrawWordBar(ImDrawListPtr dl, float contentWidth, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var barH = 20f * scale;

        var status = _automation.CurrentStatus;
        var isTyping = _automation.IsTyping;
        var isHalt = status.StartsWith("SAFETY") || status.Contains("Halt") || status.Contains("Obstruction");
        var isDuty = _automation.CurrentPhase == EventPhase.InMinigame || status.Contains("In Combat");
        var isQueue = _automation.CurrentPhase is EventPhase.InQueue or EventPhase.Commencing or EventPhase.Registering || status.Contains("Queued") || status.Contains("Commencing") || status.Contains("Registration");

        var dotColor = isHalt ? FF7RTheme.LimitRed :
                       isTyping ? FF7RTheme.MakoCyan :
                       isDuty ? FF7RTheme.MakoCyanSoft :
                       isQueue ? FF7RTheme.AtbGold :
                       FF7RTheme.TextMuted;

        // Status dot
        var dotRadius = 2.5f * scale;
        var dotCenter = new Vector2(origin.X + dotRadius + 2f * scale, origin.Y + barH * 0.5f);

        if (isTyping || isHalt)
        {
            var pulse = Motion.Pulse(isHalt ? 400.0 : 800.0);
            dl.AddCircleFilled(dotCenter, dotRadius + 2f * scale * pulse, Paint.Col(FF7RTheme.WithAlpha(dotColor, pulse * 0.35f)));
        }
        dl.AddCircleFilled(dotCenter, dotRadius, Paint.Col(dotColor));

        // Active word
        var activeWord = _automation.ActiveWord;
        var hasWord = !string.IsNullOrEmpty(activeWord);
        var displayWord = hasWord ? $"> \"{activeWord}\" <" : "> standby <";
        var wordColor = isHalt ? FF7RTheme.LimitRedSoft :
                        isTyping ? FF7RTheme.MakoCyanSoft :
                        hasWord ? FF7RTheme.TextStrong :
                        FF7RTheme.TextMuted;

        var wordPos = new Vector2(dotCenter.X + dotRadius + 6f * scale, origin.Y + (barH - ImGui.GetTextLineHeight()) * 0.5f);
        dl.AddText(wordPos, Paint.Col(wordColor), displayWord);

        // Right-aligned mini status text
        var statusLabel = isHalt ? "HALT" :
                          isTyping ? "TYPING" :
                          isDuty ? "COMBAT" :
                          "IDLE";
        var statusSize = ImGui.CalcTextSize(statusLabel);
        var statusPos = new Vector2(origin.X + contentWidth - statusSize.X, origin.Y + (barH - statusSize.Y) * 0.5f);
        dl.AddText(statusPos, Paint.Col(FF7RTheme.WithAlpha(dotColor, 0.7f)), statusLabel);

        // Advance cursor
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + barH + 4f * scale));
    }

    private static void DrawSectionLabel(ImDrawListPtr dl, string label, float contentWidth, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var textH = ImGui.GetTextLineHeight();
        var midY = origin.Y + textH * 0.5f;

        // Muted label text
        dl.AddText(origin, Paint.Col(FF7RTheme.TextMuted), label);

        // Hairline extending to the right
        var textW = ImGui.CalcTextSize(label).X;
        var lineStart = new Vector2(origin.X + textW + 6f * scale, midY);
        var lineEnd = new Vector2(origin.X + contentWidth, midY);
        if (lineEnd.X > lineStart.X)
        {
            dl.AddLine(lineStart, lineEnd, Paint.Col(FF7RTheme.Hairline), 1f);
        }

        // Advance cursor
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + textH + 4f * scale));
    }
}
