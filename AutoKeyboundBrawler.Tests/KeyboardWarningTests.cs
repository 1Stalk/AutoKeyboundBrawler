using AutoKeyboundBrawler.Interop;
using Xunit;

namespace AutoKeyboundBrawler.Tests;

public class KeyboardWarningTests
{
    private const string Sheet =
        "A usable keyboard could not be found.\r\nWithout the ability to input text, you will be unable to progress through the game.\r\nAre you sure you wish to play?";

    [Fact]
    public void Matches_SheetText_WithDifferentLineBreaks()
    {
        var rendered = "A usable keyboard could not be found.\nWithout the ability to input text, you will be unable to progress through the game.\nAre you sure you wish to play?";
        Assert.True(AddonKTGKeyboardWarning.IsKeyboardWarningText(rendered, new[] { Sheet }));
    }

    [Fact]
    public void Matches_LocalizedText_FromSheetList()
    {
        var german = "Es wurde keine verwendbare Tastatur gefunden.\r\nSind Sie sicher?";
        Assert.True(AddonKTGKeyboardWarning.IsKeyboardWarningText(german, new[] { Sheet, german }));
    }

    [Fact]
    public void Matches_EnglishFallback_WhenSheetUnavailable()
    {
        Assert.True(AddonKTGKeyboardWarning.IsKeyboardWarningText(Sheet, System.Array.Empty<string>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Exchange 910 MGC for a Keybound Coffer?")]
    [InlineData("Leave the duty?")]
    public void DoesNotMatch_UnrelatedPrompts(string? text)
    {
        Assert.False(AddonKTGKeyboardWarning.IsKeyboardWarningText(text, new[] { Sheet }));
    }
}
