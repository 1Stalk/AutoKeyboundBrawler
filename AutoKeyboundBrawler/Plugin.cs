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
    public static Configuration Configuration { get; private set; } = null!;

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

    private void OnFrameworkUpdate(IFramework framework)
    {
        _automation.Tick();
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
