using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.Interop;

/// <summary>
/// Inter-Plugin Communication (IPC) wrapper for vnavmesh.
/// Safe and resilient: never throws if vnavmesh is uninstalled or disabled.
/// </summary>
public class VNavmeshIPC
{
    private readonly ICallGateSubscriber<bool>? _navIsReadySub;
    private readonly ICallGateSubscriber<Vector3, bool, bool>? _pathfindAndMoveToSub;
    private readonly ICallGateSubscriber<object>? _pathStopSub;
    private readonly ICallGateSubscriber<bool>? _pathIsRunningSub;
    private readonly IPluginLog? _log;

    public VNavmeshIPC(IDalamudPluginInterface? pi, IPluginLog? log = null)
    {
        _log = log;
        if (pi == null) return;

        try
        {
            _navIsReadySub = pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
            _pathfindAndMoveToSub = pi.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
            _pathStopSub = pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
            _pathIsRunningSub = pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        }
        catch (Exception ex)
        {
            _log?.Verbose(ex, "[VNavmeshIPC] Failed to initialize IPC subscribers");
        }
    }

    /// <summary>
    /// Checks whether vnavmesh is installed and has a fully generated navmesh for the current zone.
    /// </summary>
    public bool IsReady
    {
        get
        {
            try
            {
                return _navIsReadySub != null && _navIsReadySub.InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Checks whether vnavmesh is currently actively executing a path.
    /// </summary>
    public bool IsPathRunning
    {
        get
        {
            try
            {
                return _pathIsRunningSub != null && _pathIsRunningSub.InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Commands vnavmesh to pathfind and move to the target destination.
    /// </summary>
    public bool PathfindAndMoveTo(Vector3 destination, bool fly = false)
    {
        try
        {
            if (_pathfindAndMoveToSub == null) return false;
            return _pathfindAndMoveToSub.InvokeFunc(destination, fly);
        }
        catch (Exception ex)
        {
            _log?.Verbose(ex, "[VNavmeshIPC] PathfindAndMoveTo failed");
            return false;
        }
    }

    /// <summary>
    /// Stops any active movement in vnavmesh.
    /// </summary>
    public void Stop()
    {
        try
        {
            _pathStopSub?.InvokeAction();
        }
        catch (Exception ex)
        {
            _log?.Verbose(ex, "[VNavmeshIPC] Stop failed");
        }
    }
}

