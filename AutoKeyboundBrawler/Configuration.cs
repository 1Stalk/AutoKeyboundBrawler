using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoKeyboundBrawler;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Master activation (automatically true if either core toggle is active)
    public bool Enabled { get; set; } = true;

    // Primary Toggle 1: In-duty Minigame Typing Automation
    public bool MinigameAutoplay { get; set; } = true;

    // Primary Toggle 2: Out-of-duty AFK Loop Automation
    public bool AutoLoopEnabled { get; set; } = false;

    // Automatic Proximity UI Open
    public bool AutoOpenNearRegistrar { get; set; } = true;

    // UI Language override (null = Auto-detect from game client)
    public Dalamud.Game.ClientLanguage? SelectedLanguage { get; set; } = null;

    // Granular AFK Loop Sub-Flags (synchronized with AutoLoopEnabled)
    public bool AutoTalkRegistrar { get; set; } = false;
    public bool AutoConfirmRegister { get; set; } = false;
    public bool AutoCommenceDuty { get; set; } = true;
    public bool AutoLeaveResults { get; set; } = true;

    // Humanized AFK Loop Delays (ms) — randomized delays between loop actions
    public int LoopDelayMinMs { get; set; } = 1200;
    public int LoopDelayMaxMs { get; set; } = 3500;

    // Typing parameters (Simulated human typing)
    public bool SimulateHumanTyping { get; set; } = true;
    public int CharDelayMinMs { get; set; } = 35;
    public int CharDelayMaxMs { get; set; } = 75;
    public int SubmissionDelayMs { get; set; } = 80;
    public bool IgnorePunctuation { get; set; } = true;

    // Safety guards
    public bool StopOnPenaltyCast { get; set; } = true;
    public bool RequireWindowFocus { get; set; } = true;

    // Targeting Priorities & Mechanics
    public bool PrioritizeAdds { get; set; } = true;
    public bool PrioritizeDefense { get; set; } = true;
    public bool AbortForDefense { get; set; } = true;
    public bool MemoryModeObscured { get; set; } = true;

    // Diagnostics
    public bool DiagnosticLogging { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        _pluginInterface = pi;

        // Combat mechanics and safety guards are always active
        StopOnPenaltyCast = true;
        RequireWindowFocus = true;
        PrioritizeAdds = true;
        PrioritizeDefense = true;
        AbortForDefense = true;
        MemoryModeObscured = true;

        // Automatically sync AutoLoopEnabled if sub-flags are already true
        if (AutoTalkRegistrar && AutoConfirmRegister)
        {
            AutoLoopEnabled = true;
        }

        Enabled = MinigameAutoplay || AutoLoopEnabled;
    }

    public void SetMinigameAutoplay(bool val)
    {
        MinigameAutoplay = val;
        Enabled = MinigameAutoplay || AutoLoopEnabled;
        Save();
    }

    public void SetAutoLoop(bool val)
    {
        AutoLoopEnabled = val;
        AutoTalkRegistrar = val;
        AutoConfirmRegister = val;
        AutoCommenceDuty = val;
        AutoLeaveResults = val;
        Enabled = MinigameAutoplay || AutoLoopEnabled;
        Save();
    }

    public void Save()
    {
        _pluginInterface?.SavePluginConfig(this);
    }
}
