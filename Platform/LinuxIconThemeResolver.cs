using System;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;

namespace Tasks.Platform;

public static class LinuxIconThemeResolver
{
    private static readonly string[] SearchRoots =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "icons"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".icons"),
        "/usr/local/share/icons",
        "/usr/share/icons"
    };

    // Standard 16x16 coordinate frame viewBox
    private static readonly StreamGeometry FallbackFrame = StreamGeometry.Parse("M 0,0 M 16,16");

    // Adwaita fallback geometries (exact 16x16 grid paths)
    private const string FallbackCloseSvg = "M4 4h1.03125c.253906.011719.511719.128906.6875.3125L8 6.59375l2.3125-2.28125c.265625-.230469.445312-.304688.6875-.3125h1v1c0 .285156-.035156.550781-.25.75L9.46875 8.03125l2.25 2.25c.1875.1875.28125.453125.28125.71875v1h-1c-.265625 0-.53125-.09375-.71875-.28125L8 9.4375l-2.28125 2.28125C5.53125 11.90625 5.265625 12 5 12H4v-1c0-.265625.09375-.53125.28125-.71875l2.28125-2.25L4.28125 5.75C4.070312 5.554688 3.976562 5.28125 4 5zm0 0";
    private const string FallbackMaximizeSvg = "M3.988281 3.992188v8.011718H12V3.992188zm2 2H10v4.011718H5.988281zm0 0";
    private const string FallbackRestoreSvg = "M4.988281 4.992188v6.011718H11V4.992188zm2 2H9v2.011718H6.988281zm0 0";
    private const string FallbackMinimizeSvg = "m4 10.0078h8v1.98828h-8z";

    public static Geometry GetCloseIcon(string themeName) =>
        ResolveGeometry(themeName, "window-close-symbolic", FallbackCloseSvg);

    public static Geometry GetMaximizeIcon(string themeName) =>
        ResolveGeometry(themeName, "window-maximize-symbolic", FallbackMaximizeSvg);

    public static Geometry GetRestoreIcon(string themeName) =>
        ResolveGeometry(themeName, "window-restore-symbolic", FallbackRestoreSvg);

    public static Geometry GetMinimizeIcon(string themeName) =>
        ResolveGeometry(themeName, "window-minimize-symbolic", FallbackMinimizeSvg);

    private static Geometry ResolveGeometry(string themeName, string iconName, string fallbackPath)
    {
        var svgFile = FindIconFile(themeName, iconName);
        if (svgFile != null && File.Exists(svgFile))
        {
            try
            {
                var content = File.ReadAllText(svgFile);
                var pathData = ExtractSvgPath(content);
                if (!string.IsNullOrWhiteSpace(pathData))
                {
                    return CreateFramedGeometry(pathData);
                }
            }
            catch
            {
                // Fallback on parse error
            }
        }

        return CreateFramedGeometry(fallbackPath);
    }

    private static Geometry CreateFramedGeometry(string pathData)
    {
        var geom = StreamGeometry.Parse(pathData);

        // Wrapping with a 16x16 frame guarantees uniform scaling and anchors minimize at the bottom
        var group = new GeometryGroup();
        group.Children.Add(FallbackFrame);
        group.Children.Add(geom);
        return group;
    }

    private static string? FindIconFile(string themeName, string iconName)
    {
        var visitedThemes = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new System.Collections.Generic.Queue<string>();
        queue.Enqueue(themeName);

        while (queue.Count > 0)
        {
            var currentTheme = queue.Dequeue();
            if (!visitedThemes.Add(currentTheme))
                continue;

            foreach (var root in SearchRoots)
            {
                var themeDir = Path.Combine(root, currentTheme);
                if (!Directory.Exists(themeDir))
                    continue;

                try
                {
                    var match = Directory.EnumerateFiles(themeDir, $"{iconName}.svg", SearchOption.AllDirectories);
                    foreach (var file in match)
                        return file;
                }
                catch
                {
                    // Ignore directory access error
                }

                var indexPath = Path.Combine(themeDir, "index.theme");
                if (File.Exists(indexPath))
                {
                    foreach (var inherited in ReadInheritedThemes(indexPath))
                    {
                        if (!visitedThemes.Contains(inherited))
                            queue.Enqueue(inherited);
                    }
                }
            }
        }

        if (!visitedThemes.Contains("Adwaita"))
            queue.Enqueue("Adwaita");
        if (!visitedThemes.Contains("hicolor"))
            queue.Enqueue("hicolor");

        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> ReadInheritedThemes(string indexThemePath)
    {
        try
        {
            foreach (var line in File.ReadLines(indexThemePath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Inherits=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = trimmed["Inherits=".Length..];
                    var parts = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var p in parts)
                        yield return p;
                }
            }
        }
        finally
        {
        }
    }

    private static string? ExtractSvgPath(string svgContent)
    {
        var matches = Regex.Matches(svgContent, @"<path[^>]*\bd=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (matches.Count == 0) return null;

        if (matches.Count == 1)
            return matches[0].Groups[1].Value;

        var combined = "";
        foreach (Match m in matches)
        {
            combined += " " + m.Groups[1].Value;
        }
        return combined.Trim();
    }
}
