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
        var layoutRaw = ReadPortalSetting("org.gnome.desktop.wm.preferences", "button-layout");
        if (string.IsNullOrWhiteSpace(layoutRaw))
        {
            layoutRaw = ReadGnomeSetting("org.gnome.desktop.wm.preferences", "button-layout");
        }
        if (string.IsNullOrWhiteSpace(layoutRaw))
        {
            layoutRaw = ReadDconfKey("/org/gnome/desktop/wm/preferences/button-layout");
        }
        if (string.IsNullOrWhiteSpace(layoutRaw))
        {
            layoutRaw = ReadDirectDconfDb("button-layout");
        }

        return ParseButtonLayout(layoutRaw);
    }

    public static string GetActiveIconTheme()
    {
        if (!WindowEnvironment.IsGnomeOrLinux)
            return "Adwaita";

        var theme = ReadPortalSetting("org.gnome.desktop.interface", "icon-theme");
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = ReadGnomeSetting("org.gnome.desktop.interface", "icon-theme");
        }
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = ReadDconfKey("/org/gnome/desktop/interface/icon-theme");
        }
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = ReadDirectDconfDb("icon-theme");
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

    private static string? ReadPortalSetting(string schema, string key)
    {
        // Try gdbus portal call first
        var val = RunCommand("gdbus", $"call --session --dest org.freedesktop.portal.Desktop --object-path /org/freedesktop/portal/desktop --method org.freedesktop.portal.Settings.Read \"{schema}\" \"{key}\"");
        if (!string.IsNullOrWhiteSpace(val))
        {
            var parsed = ParsePortalVariant(val);
            if (!string.IsNullOrWhiteSpace(parsed))
                return parsed;
        }

        // Fallback to busctl
        var busVal = RunCommand("busctl", $"--user call org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop org.freedesktop.portal.Settings Read ss \"{schema}\" \"{key}\"");
        if (!string.IsNullOrWhiteSpace(busVal))
        {
            var parsed = ParsePortalVariant(busVal);
            if (!string.IsNullOrWhiteSpace(parsed))
                return parsed;
        }

        return null;
    }

    private static string? ParsePortalVariant(string raw)
    {
        // Format from gdbus: (<<'appmenu:minimize,maximize,close'>>,) or (<<'MoreWaita'>>,)
        // Format from busctl: v v s "appmenu:minimize,maximize,close"
        var firstQuote = raw.IndexOfAny(new[] { '\'', '"' });
        var lastQuote = raw.LastIndexOfAny(new[] { '\'', '"' });
        if (firstQuote >= 0 && lastQuote > firstQuote)
        {
            return raw.Substring(firstQuote + 1, lastQuote - firstQuote - 1).Trim();
        }
        return null;
    }

    private static string? ReadDirectDconfDb(string keyName)
    {
        // Inside flatpak with ~/.config/dconf:ro mounted, dconf binary may be missing or unprivileged,
        // so read from ~/.config/dconf/user directly if available
        try
        {
            var dconfUser = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "dconf", "user");
            if (!System.IO.File.Exists(dconfUser))
                return null;

            var bytes = System.IO.File.ReadAllBytes(dconfUser);
            var content = System.Text.Encoding.ASCII.GetString(bytes);

            // Keys in dconf database are preceded by key name and null or type delimiter
            var idx = content.IndexOf(keyName, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var slice = content.Substring(idx + keyName.Length, Math.Min(120, content.Length - (idx + keyName.Length)));
                // Find first printable segment following spaces/control characters
                var match = System.Text.RegularExpressions.Regex.Match(slice, @"[a-zA-Z0-9_\-:,]+");
                if (match.Success)
                {
                    return match.Value;
                }
            }
        }
        catch
        {
            // Ignore file read error
        }
        return null;
    }

    private static string? RunCommand(string fileName, string args)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
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

    private static string? ReadGnomeSetting(string schema, string key)
    {
        return RunCommand("gsettings", $"get {schema} {key}");
    }

    private static string? ReadDconfKey(string keyPath)
    {
        return RunCommand("dconf", $"read {keyPath}");
    }
}
