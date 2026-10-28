using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AutoKeyboundBrawler.UI;

internal static class Motion
{
    public const float SwitchMs = 200f;
    public const float SwitchSlide = 8f;

    private static readonly Dictionary<int, float> Values = new();

    public static float DeltaTime => MathF.Min(ImGui.GetIO().DeltaTime, 0.05f);

    public static int Key(string id) => unchecked((int)ImGui.GetID(id));
    public static int Key(string id, int salt) => HashCode.Combine(ImGui.GetID(id), salt);

    public static float Approach(int key, float target, float speed = 16f)
    {
        if (!Values.TryGetValue(key, out var current))
        {
            Values[key] = target;
            return target;
        }

        var next = current + (target - current) * (1f - MathF.Exp(-speed * DeltaTime));
        if (MathF.Abs(next - target) < 0.0005f)
        {
            next = target;
        }

        Values[key] = next;
        return next;
    }

    public static float Hover(int key, bool hovered) => Approach(key, hovered ? 1f : 0f, 18f);

    public static float Pulse(double periodMs = 800.0)
    {
        var t = (Environment.TickCount64 % periodMs) / periodMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    public static Vector4 PulseColor(Vector4 a, Vector4 b, double periodMs = 800.0)
        => Vector4.Lerp(a, b, Pulse(periodMs));

    public static float EaseInOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    public static float EaseOutCubic(float t)
    {
        var u = 1f - t;
        return 1f - u * u * u;
    }

    public static ImRaii.StyleDisposable PushAlpha(float progress)
        => ImRaii.PushStyle(ImGuiStyleVar.Alpha, MathF.Max(0.001f, progress * ImGui.GetStyle().Alpha));

    public static ImRaii.StyleDisposable PushReveal(float progress, float slide = SwitchSlide)
    {
        if (progress < 1f)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1f - progress) * slide * ImGuiHelpers.GlobalScale);
        }

        return PushAlpha(progress);
    }
}

