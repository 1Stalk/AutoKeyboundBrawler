using AutoKeyboundBrawler.Interop;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.Core;

/// <summary>
/// High-level navigation manager orchestrating character movement in Keybound Square.
/// Automatically leverages vnavmesh IPC when available, with direct steering fallback.
/// </summary>
public class KTGNavigationManager
{
    private readonly VNavmeshIPC _vnav;
    private readonly Configuration _config;
    private readonly IPluginLog? _log;

    private Vector3? _currentDestination;
    private float _stopDistance = 3.0f;
    private DateTime _navigationStartTime = DateTime.MinValue;
    private const int MaxNavigationTimeoutSec = 15;

    public bool IsNavigating => _currentDestination.HasValue;
    public bool IsVNavReady => _vnav.IsReady;

    public KTGNavigationManager(IDalamudPluginInterface? pi, Configuration config, IPluginLog? log = null)
    {
        _config = config;
        _log = log;
        _vnav = new VNavmeshIPC(pi, log);
    }

    /// <summary>
    /// Starts navigation towards the specified world coordinate.
    /// </summary>
    public bool NavigateTo(Vector3 destination, float stopDistance = 3.0f)
    {
        var localPlayer = Svc.Objects?.LocalPlayer;
        if (localPlayer == null) return false;

        var dist = Vector3.Distance(localPlayer.Position, destination);
        if (dist <= stopDistance)
        {
            // Already at destination
            Stop();
            return true;
        }

        _currentDestination = destination;
        _stopDistance = stopDistance;
        _navigationStartTime = DateTime.UtcNow;

        if (_vnav.IsReady)
        {
            _log?.Information($"[KTGNavigation] Moving to {destination} via vnavmesh (distance: {dist:F1}y)");
            return _vnav.PathfindAndMoveTo(destination, false);
        }

        _log?.Information($"[KTGNavigation] Moving to {destination} via direct steering (distance: {dist:F1}y, vnavmesh not ready)");
        return true;
    }

    /// <summary>
    /// Updates navigation progress per frame/tick. Returns true if destination is reached.
    /// </summary>
    public bool Update()
    {
        if (!_currentDestination.HasValue)
        {
            return true;
        }

        var localPlayer = Svc.Objects?.LocalPlayer;
        if (localPlayer == null)
        {
            Stop();
            return false;
        }

        var dest = _currentDestination.Value;
        var dist = Vector3.Distance(localPlayer.Position, dest);

        // Check if destination reached
        if (dist <= _stopDistance)
        {
            _log?.Information($"[KTGNavigation] Arrived at destination ({dist:F1}y <= {_stopDistance:F1}y). Stopping.");
            Stop();
            return true;
        }

        // Safety timeout
        if ((DateTime.UtcNow - _navigationStartTime).TotalSeconds > MaxNavigationTimeoutSec)
        {
            _log?.Warning($"[KTGNavigation] Navigation timed out after {MaxNavigationTimeoutSec}s. Stopping.");
            Stop();
            return true;
        }

        // If vnavmesh is active, check if it finished pathing
        if (_vnav.IsReady)
        {
            if (!_vnav.IsPathRunning && dist > _stopDistance)
            {
                // If stopped early but not yet within stop distance, re-request path once per second
                if ((DateTime.UtcNow - _navigationStartTime).TotalMilliseconds > 1000)
                {
                    _vnav.PathfindAndMoveTo(dest, false);
                }
            }
            return false;
        }

        // Direct steering fallback
        try
        {
            unsafe
            {
                var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)localPlayer.Address;
                if (chara != null)
                {
                    var dx = dest.X - localPlayer.Position.X;
                    var dz = dest.Z - localPlayer.Position.Z;
                    var desiredAngle = MathF.Atan2(dx, dz);
                    chara->GameObject.Rotation = desiredAngle;
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Verbose(ex, "[KTGNavigation] Direct steering step threw");
        }

        return false;
    }

    /// <summary>
    /// Immediately halts active navigation.
    /// </summary>
    public void Stop()
    {
        _currentDestination = null;
        if (_vnav.IsReady)
        {
            _vnav.Stop();
        }
    }
}

