using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tasks.Platform;

public enum WindowButtonKind
{
    Close,
    Minimize,
    Maximize
}

public class ButtonLayoutConfiguration
{
    public IReadOnlyList<WindowButtonKind> LeftButtons { get; init; } = Array.Empty<WindowButtonKind>();
    public IReadOnlyList<WindowButtonKind> RightButtons { get; init; } = Array.Empty<WindowButtonKind>();
}

public static class GnomeDesktopService
{
    public static ButtonLayoutConfiguration GetButtonLayout()
    {
        if (WindowEnvironment.IsWindows)
        {
            return new ButtonLayoutConfiguration
            {
                LeftButtons = Array.Empty<WindowButtonKind>(),
                RightButtons = new[] { WindowButtonKind.Minimize, WindowButtonKind.Maximize, WindowButtonKind.Close }
            };
        }

        if (WindowEnvironment.IsMacOS)
        {
            return new ButtonLayoutConfiguration
            {
                LeftButtons = new[] { WindowButtonKind.Close, WindowButtonKind.Minimize, WindowButtonKind.Maximize },
                RightButtons = Array.Empty<WindowButtonKind>()
            };
        }

        // GNOME / Linux
        var layoutRaw = ReadGnomeSetting("org.gnome.desktop.wm.preferences", "button-layout");
        if (string.IsNullOrWhiteSpace(layoutRaw))
        {
            layoutRaw = ReadDconfKey("/org/gnome/desktop/wm/preferences/button-layout");
        }

        return ParseButtonLayout(layoutRaw);
    }

    public static string GetActiveIconTheme()
    {
        if (!WindowEnvironment.IsGnomeOrLinux)
            return "Adwaita";

        var theme = ReadGnomeSetting("org.gnome.desktop.interface", "icon-theme");
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = ReadDconfKey("/org/gnome/desktop/interface/icon-theme");
        }

        theme = theme?.Trim('\'', '"', ' ', '\r', '\n') ?? "";
        return string.IsNullOrWhiteSpace(theme) ? "Adwaita" : theme;
    }

    public static ButtonLayoutConfiguration ParseButtonLayout(string? rawLayout)
    {
        if (string.IsNullOrWhiteSpace(rawLayout))
        {
            // GNOME default fallback
            return new ButtonLayoutConfiguration
            {
                LeftButtons = Array.Empty<WindowButtonKind>(),
                RightButtons = new[] { WindowButtonKind.Minimize, WindowButtonKind.Maximize, WindowButtonKind.Close }
            };
        }

        var cleaned = rawLayout.Trim('\'', '"', ' ', '\r', '\n');
        var parts = cleaned.Split(':', 2);

        var left = ParseTokens(parts[0]);
        var right = parts.Length > 1 ? ParseTokens(parts[1]) : Array.Empty<WindowButtonKind>();

        return new ButtonLayoutConfiguration
        {
            LeftButtons = left,
            RightButtons = right
        };
    }

    private static IReadOnlyList<WindowButtonKind> ParseTokens(string part)
    {
        var list = new List<WindowButtonKind>();
        var tokens = part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (string.Equals(token, "close", StringComparison.OrdinalIgnoreCase))
                list.Add(WindowButtonKind.Close);
            else if (string.Equals(token, "minimize", StringComparison.OrdinalIgnoreCase))
                list.Add(WindowButtonKind.Minimize);
            else if (string.Equals(token, "maximize", StringComparison.OrdinalIgnoreCase))
                list.Add(WindowButtonKind.Maximize);
        }
        return list;
    }

    private static string? ReadGnomeSetting(string schema, string key)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = $"get {schema} {key}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);
            return proc.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadDconfKey(string keyPath)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dconf",
                    Arguments = $"read {keyPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);
            return proc.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
