using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using AutoKeyboundBrawler.UI;
using System;
using System.Linq;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace AutoKeyboundBrawler.Tests;

public class AutoBuyCoffersTests
{
    private readonly ITestOutputHelper _output;

    public AutoBuyCoffersTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestKTGLocalizationRegistry_TraderIdentification()
    {
        Assert.True(KTGLocalizationRegistry.IsTrader(1058753, "Any name"));
        Assert.True(KTGLocalizationRegistry.IsTrader(0, "MGC trader"));
        Assert.True(KTGLocalizationRegistry.IsTrader(0, "参加賞交換"));
        Assert.True(KTGLocalizationRegistry.IsTrader(0, "MGC-Händlerin"));
        Assert.True(KTGLocalizationRegistry.IsTrader(0, "négociante en crédits du Gold Saucer"));
        Assert.False(KTGLocalizationRegistry.IsTrader(999, "Random NPC"));
    }

    [Fact]
    public void TestKeyboundCoffer_ItemConstants()
    {
        Assert.Equal(52450u, KTGLocalizationRegistry.KeyboundCofferItemId);
        Assert.Equal(910u, KTGLocalizationRegistry.KeyboundCofferCost);
        Assert.Equal(1058753u, KTGLocalizationRegistry.MgcTraderDataId);
        Assert.Equal(52322u, KTGCurrencyReader.MgcItemId);
    }

    [Fact]
    public void TestWaypoints_Defined()
    {
        Assert.NotEqual(Vector3.Zero, KTGLocalizationRegistry.RegistrarWaypoint);
        Assert.NotEqual(Vector3.Zero, KTGLocalizationRegistry.TraderWaypoint);
        Assert.True(Vector3.Distance(KTGLocalizationRegistry.RegistrarWaypoint, KTGLocalizationRegistry.TraderWaypoint) > 10.0f);
    }

    [Fact]
    public void TestVNavmeshIPC_SafeWhenNull()
    {
        var ipc = new VNavmeshIPC(null, null);
        Assert.False(ipc.IsReady);
        Assert.False(ipc.IsPathRunning);
        Assert.False(ipc.PathfindAndMoveTo(Vector3.Zero));
        // Must not throw
        ipc.Stop();
    }

    [Fact]
    public void TestKTGNavigationManager_Lifecycle()
    {
        var config = new Configuration();
        var nav = new KTGNavigationManager(null, config, null);

        Assert.False(nav.IsNavigating);
        nav.Stop();
        Assert.False(nav.IsNavigating);
    }

    [Fact]
    public void TestAutoBuyCoffers_StateTriggerLogic()
    {
        var config = new Configuration
        {
            AutoLoopEnabled = true,
            AutoBuyCoffers = true,
            AutoBuyThreshold = 9500
        };

        // When MGC < threshold, should not trigger
        KTGCurrencyReader.SetOverridesForTesting(9499, 10000);
        var (count, _) = KTGCurrencyReader.GetMGC();
        Assert.True(count < (uint)config.AutoBuyThreshold);

        // When MGC >= threshold, should trigger
        KTGCurrencyReader.SetOverridesForTesting(9500, 10000);
        (count, _) = KTGCurrencyReader.GetMGC();
        Assert.True(count >= (uint)config.AutoBuyThreshold);

        KTGCurrencyReader.SetOverridesForTesting(null, null);
    }

    [Fact]
    public void TestAddonKTGShop_Constants()
    {
        Assert.Equal("SelectYesno", AddonKTGShop.SelectYesnoAddonName);
        Assert.Equal("ShopExchangeCurrency", AddonKTGShop.ShopExchangeCurrencyAddonName);
        Assert.Equal("ShopExchangeCurrencyDialog", AddonKTGShop.ShopExchangeCurrencyDialogAddonName);
    }

    [Fact]
    public void TestVNavmesh_LocalizationStatusStrings()
    {
        Assert.False(string.IsNullOrEmpty(Loc.Get("VnavReady")));
        Assert.False(string.IsNullOrEmpty(Loc.Get("VnavNotReady")));
        Assert.Contains("vnavmesh", Loc.Get("VnavReady"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vnavmesh", Loc.Get("VnavNotReady"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestConfiguration_DefaultValues()
    {
        var config = new Configuration();

        // Main Toggles
        Assert.True(config.MinigameAutoplay);
        Assert.False(config.AutoLoopEnabled);
        Assert.True(config.AutoOpenNearRegistrar);
        Assert.False(config.AutoBuyCoffers);

        // Speed Delays
        Assert.Equal(75, config.CharDelayMinMs);
        Assert.Equal(180, config.CharDelayMaxMs);
        Assert.Equal(105, config.SubmissionDelayMs);
        Assert.True(config.IgnorePunctuation);

        // Language
        Assert.Null(config.SelectedLanguage);
    }
}
