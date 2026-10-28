using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
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

    private static readonly Vector2 DefaultSize = new(430, 485);
    private static readonly WindowSizeConstraints CompactConstraints = new()
    {
        MinimumSize = new Vector2(360, HeaderBar.Height),
        MaximumSize = new Vector2(float.MaxValue, HeaderBar.Height),
    };
    private static readonly WindowSizeConstraints ExpandedConstraints = new()
    {
        MinimumSize = new Vector2(380, 420),
        MaximumSize = new Vector2(620, 720),
    };

    private readonly Configuration _config;
    private readonly KTGAutomationManager _automation;

    private bool _compact;
    private float _collapse;
    private float _collapseFrom;
    private long _collapseTick;
    private bool _sizeDriven;
    private Vector2 _expandedSize = DefaultSize;
    private int _selectedTab;
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
        var bodyTop = windowPos.Y + headerHeight + 8f * scale;
        var bodyPaddingX = 12f * scale;
        var contentWidth = windowSize.X - bodyPaddingX * 2f;
        var bodyHeight = windowSize.Y - headerHeight - 16f * scale;

        if (bodyHeight < 10f) return;

        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + bodyPaddingX, bodyTop));

        using (Motion.PushAlpha(MathF.Max(0.001f, 1f - _collapse)))
        using (var bodyChild = ImRaii.Child("##akb_body", new Vector2(contentWidth, bodyHeight), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (!bodyChild) return;

            // 1. Battle Telemetry Card (Live HUD)
            BattleTelemetryCard.Draw(_automation);

            // 2. Primary Automation Controls
            using (TacticalCard.Begin())
            {
                var minigame = _config.MinigameAutoplay;
                if (FF7ToggleSwitch.DrawRow("##minigame_solver", "Combat Typing Solver", "Humanized typing, shield detection & adds priority", ref minigame,
                    FF7RTheme.MakoCyan, "Automatically solves enemy phrases letter-by-letter with humanized typing delays."))
                {
                    _config.SetMinigameAutoplay(minigame);
                }

                var autoLoop = _config.AutoLoopEnabled;
                if (FF7ToggleSwitch.DrawRow("##afk_loop", "AFK Duty Loop Runner", "Auto-register, commence & exit results", ref autoLoop,
                    FF7RTheme.AtbGold, "Automatically interacts with Registrar, confirms entry, commences duty, and exits results."))
                {
                    _config.SetAutoLoop(autoLoop);
                }
            }

            // 3. Segmented Navigation Tabs
            _selectedTab = SegmentedTabs.Draw("##akb_tabs", _selectedTab, ["⚡ SPEED TUNING", "🛡 COMBAT RULES"]);

            if (_selectedTab == 0)
            {
                // Speed & Typing Tuning Tab
                using (TacticalCard.Begin())
                {
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
                    if (TacticalSlider.Draw("sub_delay", "Enter Submission Delay", ref enterDelay, 20, 300))
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
            else
            {
                // Combat Rules & Priorities Tab
                using (TacticalCard.Begin())
                {
                    var stopPenalty = _config.StopOnPenaltyCast;
                    if (FF7ToggleSwitch.DrawRow("##stop_penalty", "Red Keyboard Obstruction Halt", "Strictly pause typing on obstruction cast", ref stopPenalty,
                        FF7RTheme.LimitRed, "Stops all typing simulation instantly when enemy casts Red Keyboard Obstruction to prevent death."))
                    {
                        _config.StopOnPenaltyCast = stopPenalty;
                        _config.Save();
                    }

                    var prioAdds = _config.PrioritizeAdds;
                    if (FF7ToggleSwitch.DrawRow("##prio_adds", "Prioritize Add Waves", "Focus add mobs before boss mechanics", ref prioAdds,
                        FF7RTheme.MakoCyan, "Solves add mobs first to clear lethal room mechanics before switching to boss."))
                    {
                        _config.PrioritizeAdds = prioAdds;
                        _config.Save();
                    }

                    var prioDefense = _config.PrioritizeDefense;
                    if (FF7ToggleSwitch.DrawRow("##prio_defense", "Prioritize Defense Quotes", "Raise shield against tankbuster phrases", ref prioDefense,
                        FF7RTheme.MakoCyan, "Immediately switches typing priority to defensive quotes when a tankbuster is imminent."))
                    {
                        _config.PrioritizeDefense = prioDefense;
                        _config.Save();
                    }

                    var memoryMode = _config.MemoryModeObscured;
                    if (FF7ToggleSwitch.DrawRow("##memory_mode", "Memory Mode (Obscured Words)", "Solve hidden/scrambled words from Lumina database", ref memoryMode,
                        FF7RTheme.MakoCyan, "Reconstructs obscured or ??? words from the Lumina Excel database."))
                    {
                        _config.MemoryModeObscured = memoryMode;
                        _config.Save();
                    }
                }
            }

            // 4. Subtle Tactical Footer
            ImGui.Dummy(new Vector2(0, 4f * scale));
            var footerStart = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            dl.AddLine(footerStart, new Vector2(footerStart.X + contentWidth, footerStart.Y), Paint.Col(FF7RTheme.Hairline));

            var footerText = "/akb  •  v1.0.0";
            var fSize = ImGui.CalcTextSize(footerText);
            var fPos = new Vector2(footerStart.X + (contentWidth - fSize.X) * 0.5f, footerStart.Y + 6f * scale);
            dl.AddText(fPos, Paint.Col(FF7RTheme.TextMuted), footerText);
        }
    }
}
