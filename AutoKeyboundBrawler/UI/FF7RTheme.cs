using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace AutoKeyboundBrawler.UI;

internal static class FF7RTheme
{
    // FF7 Remake inspired color palette
    // Deep slate / gunmetal surfaces
    public static readonly Vector4 WindowBg         = new(0.038f, 0.052f, 0.072f, 0.94f);
    public static readonly Vector4 Surface0         = new(0.058f, 0.078f, 0.108f, 1.000f);
    public static readonly Vector4 Surface1         = new(0.078f, 0.108f, 0.148f, 1.000f);
    public static readonly Vector4 Surface2         = new(0.110f, 0.150f, 0.200f, 1.000f);
    public static readonly Vector4 Surface3         = new(0.150f, 0.205f, 0.268f, 1.000f);

    // Mako / Materia Cyan & Neon accents
    public static readonly Vector4 MakoCyan         = new(0.000f, 0.949f, 0.839f, 1.000f); // #00F2D6
    public static readonly Vector4 MakoCyanSoft     = new(0.380f, 1.000f, 0.910f, 1.000f);
    public static readonly Vector4 MakoCyanDeep     = new(0.000f, 0.580f, 0.520f, 1.000f);
    public static readonly Vector4 MakoCyanGlow     = new(0.000f, 0.949f, 0.839f, 0.220f);

    // ATB Gold / Amber
    public static readonly Vector4 AtbGold          = new(1.000f, 0.760f, 0.240f, 1.000f); // #FFC23D
    public static readonly Vector4 AtbGoldSoft      = new(1.000f, 0.860f, 0.480f, 1.000f);

    // Limit / Danger Red
    public static readonly Vector4 LimitRed         = new(1.000f, 0.280f, 0.380f, 1.000f); // #FF4761
    public static readonly Vector4 LimitRedSoft     = new(1.000f, 0.480f, 0.550f, 1.000f);

    // Text & Typographic Hierarchy
    public static readonly Vector4 TextStrong       = new(0.960f, 0.980f, 1.000f, 1.000f);
    public static readonly Vector4 TextSecondary    = new(0.680f, 0.760f, 0.850f, 1.000f);
    public static readonly Vector4 TextMuted        = new(0.420f, 0.490f, 0.580f, 1.000f);
    public static readonly Vector4 InkOnMako        = new(0.020f, 0.080f, 0.080f, 1.000f);

    // Tech Borders & Accents
    public static readonly Vector4 BorderDim        = new(0.180f, 0.250f, 0.340f, 0.800f);
    public static readonly Vector4 BorderGlow       = new(0.000f, 0.949f, 0.839f, 0.550f);
    public static readonly Vector4 Hairline         = new(1.000f, 1.000f, 1.000f, 0.075f);
    public static readonly Vector4 Highlight        = new(1.000f, 1.000f, 1.000f, 0.120f);

    // Tactical Geometry - Sharp, modern FF7R bevels
    public const float WindowRounding = 8f;
    public const float CardRounding   = 4f;
    public const float FrameRounding  = 3f;

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    public static Vector4 Tint(Vector4 baseColor, Vector4 accent, float amount)
        => Vector4.Lerp(baseColor, accent, amount) with { W = baseColor.W };

    public static IDisposable PushChrome(Vector2 windowPadding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowRounding * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.WindowPadding, windowPadding * scale)
            .Push(ImGuiStyleVar.ChildRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.ChildBorderSize, 0f)
            .Push(ImGuiStyleVar.PopupRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f)
            .Push(ImGuiStyleVar.FrameRounding, FrameRounding * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(6f, 3f) * scale)
            .Push(ImGuiStyleVar.FrameBorderSize, 1f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 4f) * scale)
            .Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(4f, 2f) * scale)
            .Push(ImGuiStyleVar.ScrollbarSize, 7f * scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, 4f * scale)
            .Push(ImGuiStyleVar.GrabRounding, 3f * scale)
            .Push(ImGuiStyleVar.GrabMinSize, 10f * scale);

        var color = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Surface1 with { W = 0.985f })
            .Push(ImGuiCol.Border, BorderDim)
            .Push(ImGuiCol.BorderShadow, Vector4.Zero)
            .Push(ImGuiCol.FrameBg, Surface1)
            .Push(ImGuiCol.FrameBgHovered, Surface2)
            .Push(ImGuiCol.FrameBgActive, Surface3)
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, new Vector4(1f, 1f, 1f, 0.12f))
            .Push(ImGuiCol.ScrollbarGrabHovered, new Vector4(1f, 1f, 1f, 0.22f))
            .Push(ImGuiCol.ScrollbarGrabActive, MakoCyan with { W = 0.6f })
            .Push(ImGuiCol.Button, Surface1)
            .Push(ImGuiCol.ButtonHovered, Surface2)
            .Push(ImGuiCol.ButtonActive, Tint(Surface2, MakoCyan, 0.35f))
            .Push(ImGuiCol.Header, Tint(Surface1, MakoCyan, 0.25f))
            .Push(ImGuiCol.HeaderHovered, Surface2)
            .Push(ImGuiCol.HeaderActive, Tint(Surface2, MakoCyan, 0.35f))
            .Push(ImGuiCol.CheckMark, MakoCyan)
            .Push(ImGuiCol.SliderGrab, MakoCyan)
            .Push(ImGuiCol.SliderGrabActive, MakoCyanSoft)
            .Push(ImGuiCol.Text, TextStrong)
            .Push(ImGuiCol.TextDisabled, TextMuted)
            .Push(ImGuiCol.Separator, Hairline)
            .Push(ImGuiCol.ResizeGrip, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripHovered, Vector4.Zero)
            .Push(ImGuiCol.ResizeGripActive, Vector4.Zero)
            .Push(ImGuiCol.TextSelectedBg, WithAlpha(MakoCyan, 0.35f));

        return new ChromeScope(style, color);
    }

    private sealed class ChromeScope(IDisposable style, IDisposable color) : IDisposable
    {
        public void Dispose()
        {
            color.Dispose();
            style.Dispose();
        }
    }
}

