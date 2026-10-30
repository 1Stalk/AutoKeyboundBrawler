using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.UI;
using Dalamud.Game.Command;
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

    private bool _hasTriggeredProximityOpen;
    private long _lastProximityCheckTick;

    private void OnFrameworkUpdate(IFramework framework)
    {
        CheckRegistrarProximity();
        _automation.Tick();
    }

    private void CheckRegistrarProximity()
    {
        if (!Configuration.AutoOpenNearRegistrar)
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
        if (!Svc.ClientState.IsLoggedIn ||
            Svc.Objects.LocalPlayer == null ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
        {
            _hasTriggeredProximityOpen = false;
            return;
        }

        // If inside Duty, reset flag so it re-triggers when returning to Square
        if (KTGSafetyGuard.IsInEventDuty ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty])
        {
            _hasTriggeredProximityOpen = false;
            return;
        }

        var registrar = KTGLocalizationRegistry.FindNearbyRegistrar(12.0f, out var dist);
        if (registrar != null)
        {
            if (dist <= 7.0f)
            {
                if (!_hasTriggeredProximityOpen)
                {
                    _hasTriggeredProximityOpen = true;
                    if (!_mainWindow.IsOpen)
                    {
                        _mainWindow.IsOpen = true;
                        Svc.Log.Information($"[AutoKeyboundBrawler] Keybound Registrar in interaction range ({dist:F1}y). Auto-opened interface.");
                    }
                }
            }
            else if (dist > 9.5f)
            {
                // Hysteresis: reset trigger once player walks away
                _hasTriggeredProximityOpen = false;
            }
        }
        else
        {
            _hasTriggeredProximityOpen = false;
        }
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
