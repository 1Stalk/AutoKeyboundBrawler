using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.UI;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using System;

namespace AutoKeyboundBrawler;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "AutoKeyboundBrawler";
    public const string CommandName = "/akb";
    public const string AltCommandName = "/autokeybound";
    public const string LongCommandName = "/autokeyboundbrawler";

    public static Plugin Instance { get; private set; } = null!;
    public static Configuration Configuration { get; internal set; } = null!;

    public readonly WindowSystem WindowSystem = new("AutoKeyboundBrawler");
    private readonly MainWindow _mainWindow;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly KTGAutomationManager _automation;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        _pluginInterface = pluginInterface;

        // Initialize ECommons
        ECommonsMain.Init(pluginInterface, this, Module.All);

        // Configuration
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface);

        // Load Word Database from Lumina KTGTypeWordTextData sheet
        KTGWordDatabase.Load(Svc.Data, Svc.Log);

        // Core Automation Manager
        _automation = new KTGAutomationManager(Configuration, Svc.Log);

        // UI Window
        _mainWindow = new MainWindow(Configuration, _automation);
        WindowSystem.AddWindow(_mainWindow);

        // Register UI builders
        _pluginInterface.UiBuilder.Draw += DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi += ToggleUI;
        _pluginInterface.UiBuilder.OpenMainUi += ToggleUI;
        ConfigureUiBuilder(_pluginInterface.UiBuilder);

        // Commands
        var commandInfo = new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the AutoKeyboundBrawler tactical interface."
        };
        Svc.Commands.AddHandler(CommandName, commandInfo);
        Svc.Commands.AddHandler(AltCommandName, commandInfo);
        Svc.Commands.AddHandler(LongCommandName, commandInfo);

        // Framework update loop
        Svc.Framework.Update += OnFrameworkUpdate;

        Svc.Log.Information("[AutoKeyboundBrawler] Plugin initialized successfully.");
    }

    internal bool _hasTriggeredProximityOpen;
    internal long _lastProximityCheckTick;

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            CheckRegistrarProximity();
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose(ex, "[AutoKeyboundBrawler] CheckRegistrarProximity threw");
        }

        try
        {
            _automation.Tick();
        }
        catch (Exception ex)
        {
            Svc.Log?.Error(ex, "[AutoKeyboundBrawler] Automation tick threw");
        }
    }

    internal void CheckRegistrarProximity()
    {
        if (Configuration == null || !Configuration.AutoOpenNearRegistrar)
        {
            return;
        }

        // Throttle check to every 250ms
        var now = Environment.TickCount64;
        if (now - _lastProximityCheckTick < 250)
        {
            return;
        }
        _lastProximityCheckTick = now;

        // If in loading screen, disconnected, or between areas, reset trigger
        if (Svc.ClientState == null || !Svc.ClientState.IsLoggedIn ||
            Svc.Objects?.LocalPlayer == null ||
            (Svc.Condition != null && Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]))
        {
            _hasTriggeredProximityOpen = false;
            return;
        }

        // If inside Duty, reset flag so it re-triggers when returning to Square
        if (KTGSafetyGuard.IsInEventDuty ||
            (Svc.Condition != null && Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]))
        {
            _hasTriggeredProximityOpen = false;
            return;
        }

        try
        {
            var registrar = KTGLocalizationRegistry.FindNearbyRegistrar(12.0f, out var dist);
            if (registrar != null)
            {
                if (UpdateProximityState(ref _hasTriggeredProximityOpen, dist, _mainWindow?.IsOpen ?? false, out var shouldOpen))
                {
                    if (_mainWindow != null && shouldOpen)
                    {
                        _mainWindow.IsOpen = true;
                        Svc.Log?.Information($"[AutoKeyboundBrawler] Keybound Registrar in interaction range ({dist:F1}y). Auto-opened interface.");
                    }
                }
            }
            else
            {
                _hasTriggeredProximityOpen = false;
            }
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose(ex, "[AutoKeyboundBrawler] CheckRegistrarProximity search failed");
            _hasTriggeredProximityOpen = false;
        }
    }

    internal static bool UpdateProximityState(ref bool hasTriggered, float distance, bool isCurrentlyOpen, out bool shouldOpen)
    {
        shouldOpen = false;
        if (distance <= 7.0f)
        {
            if (!hasTriggered)
            {
                hasTriggered = true;
                if (!isCurrentlyOpen)
                {
                    shouldOpen = true;
                    return true;
                }
            }
        }
        else if (distance > 9.5f)
        {
            // Hysteresis: reset trigger once player walks away
            hasTriggered = false;
        }

        return false;
    }

    internal static void ConfigureUiBuilder(IUiBuilder uiBuilder)
    {
        if (uiBuilder == null) return;
        uiBuilder.DisableCutsceneUiHide = true;
        uiBuilder.DisableAutomaticUiHide = true;
    }

    public void OpenUI()
    {
        _mainWindow.IsOpen = true;
    }

    public bool IsUIOpen
    {
        get => _mainWindow.IsOpen;
        set => _mainWindow.IsOpen = value;
    }

    private void OnCommand(string command, string args)
    {
        ToggleUI();
    }

    private void ToggleUI()
    {
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Commands.RemoveHandler(CommandName);
        Svc.Commands.RemoveHandler(AltCommandName);
        Svc.Commands.RemoveHandler(LongCommandName);

        _pluginInterface.UiBuilder.Draw -= DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleUI;
        _pluginInterface.UiBuilder.OpenMainUi -= ToggleUI;

        WindowSystem.RemoveAllWindows();
        _mainWindow.Dispose();

        _automation.Reset();
        ECommonsMain.Dispose();

        Svc.Log.Information("[AutoKeyboundBrawler] Plugin disposed.");
    }
}
