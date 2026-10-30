using System;

namespace AutoKeyboundBrawler.Core;

/// <summary>
/// Authoritative reader for event special currencies (such as Manderville Gold Saucer Credits).
/// Directly queries native CurrencyManager with safe fallback and cache throttling.
/// </summary>
public static unsafe class KTGCurrencyReader
{
    /// <summary>
    /// Lumina Item Sheet Row ID for Manderville Gold Saucer Credits (MGC).
    /// </summary>
    public const uint MgcItemId = 52322;

    /// <summary>
    /// Default maximum capacity for Manderville Gold Saucer Credits.
    /// </summary>
    public const uint DefaultMaxCount = 10000;

    private const long CacheLifetimeMs = 250;

    private static uint? _countOverride;
    private static uint? _maxOverride;

    private static uint _cachedCount;
    private static uint _cachedMax = DefaultMaxCount;
    private static long _lastPollTicks;

    /// <summary>
    /// Overrides MGC values for testing or simulation.
    /// Pass null to clear overrides and resume native querying.
    /// </summary>
    internal static void SetOverridesForTesting(uint? count, uint? max = null)
    {
        _countOverride = count;
        _maxOverride = max;
    }

    /// <summary>
    /// Clears internal cache state for testing.
    /// </summary>
    internal static void ResetCacheForTesting()
    {
        _cachedCount = 0;
        _cachedMax = DefaultMaxCount;
        _lastPollTicks = 0;
    }

    /// <summary>
    /// Returns the player's current Manderville Gold Saucer Credits (MGC) balance and cap.
    /// </summary>
    public static (uint Count, uint Max) GetMGC()
    {
        if (_countOverride.HasValue || _maxOverride.HasValue)
        {
            return (_countOverride ?? _cachedCount, _maxOverride ?? DefaultMaxCount);
        }

        var now = Environment.TickCount64;
        if (now - _lastPollTicks < CacheLifetimeMs && _lastPollTicks > 0)
        {
            return (_cachedCount, _cachedMax);
        }

        try
        {
            var cm = FFXIVClientStructs.FFXIV.Client.Game.CurrencyManager.Instance();
            if (cm != null)
            {
                var count = cm->GetItemCount(MgcItemId);
                var max = cm->GetItemMaxCount(MgcItemId);

                _cachedCount = count;
                _cachedMax = max > 0 ? max : DefaultMaxCount;
                _lastPollTicks = now;

                return (_cachedCount, _cachedMax);
            }
        }
        catch
        {
            // CurrencyManager not initialized or called out-of-process
        }

        return (_cachedCount, _cachedMax);
    }

    /// <summary>
    /// Gets the current MGC count.
    /// </summary>
    public static uint CurrentCount => GetMGC().Count;

    /// <summary>
    /// Gets the maximum MGC capacity.
    /// </summary>
    public static uint MaxCount => GetMGC().Max;

    /// <summary>
    /// Formats the MGC display string (e.g. "2,327 MGC").
    /// </summary>
    public static string FormatDisplay(uint count) => $"{count:N0} MGC";

    /// <summary>
    /// Formats the MGC tooltip string (e.g. "Manderville Gold Saucer Credits (MGC): 2,327 / 10,000").
    /// </summary>
    public static string FormatTooltip(string tooltipLabel, uint count, uint max)
        => $"{tooltipLabel}: {count:N0} / {max:N0}";
}
