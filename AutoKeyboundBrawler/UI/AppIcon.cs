using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.DalamudServices;
using System;
using System.IO;
using System.Numerics;

namespace AutoKeyboundBrawler.UI;

internal static class AppIcon
{
    private const string FileName = "icon.png";
    private static string? _iconPath;
    private static bool? _iconExists;

    private static string ResolveIconPath()
    {
        if (_iconPath != null) return _iconPath;

        var assemblyDir = Svc.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;
        var p1 = Path.Combine(assemblyDir, "images", FileName);
        if (File.Exists(p1)) return _iconPath = p1;

        var p2 = Path.Combine(assemblyDir, FileName);
        if (File.Exists(p2)) return _iconPath = p2;

        var p3 = Path.Combine(AppContext.BaseDirectory, "images", FileName);
        if (File.Exists(p3)) return _iconPath = p3;

        return _iconPath = p1;
    }

    public static void Draw(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float alpha = 1.0f)
    {
        var path = ResolveIconPath();
        _iconExists ??= File.Exists(path);

        if (_iconExists.Value)
        {
            var texture = Svc.Texture.GetFromFile(path).GetWrapOrEmpty();
            dl.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
                Paint.Col(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        // High-tech fallback icon
        Paint.Gradient(dl, min, max, FF7RTheme.MakoCyanSoft, FF7RTheme.MakoCyan, rounding);
        var center = (min + max) * 0.5f;
        var iconStr = FontAwesomeIcon.Keyboard.ToIconString();
        var size = ImGui.CalcTextSize(iconStr);
        dl.AddText(center - size * 0.5f, Paint.Col(FF7RTheme.WithAlpha(FF7RTheme.TextStrong, alpha)), iconStr);
    }
}

