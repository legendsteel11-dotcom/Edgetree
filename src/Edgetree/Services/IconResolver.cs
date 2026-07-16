using System.IO;
using System.Text.Json;
using SidebarExplorer.App.Models;
using Application = System.Windows.Application;

namespace SidebarExplorer.App.Services;

public static class IconResolver
{
    private const string IconBasePath = "pack://application:,,,/Resources/Icons/";
    private const string FallbackFileIcon = "file.png";
    private const string FallbackFolderIcon = "folder.png";
    private const string FallbackFolderOpenIcon = "folder-open.png";

    private static readonly Lazy<IconMap> Map = new(LoadMap);

    public static string Resolve(FileSystemItem item)
    {
        string? png = item.IsDirectory ? ResolveFolder(item) : ResolveFile(item);
        return IconBasePath + png;
    }

    private static string ResolveFolder(FileSystemItem item)
    {
        var map = Map.Value;
        string nameLower = item.Name.ToLowerInvariant();
        var table = item.IsExpanded ? map.FolderNamesExpanded : map.FolderNames;

        if (table.TryGetValue(nameLower, out var png))
        {
            return png;
        }

        var fallback = item.IsExpanded ? map.DefaultFolderExpanded : map.DefaultFolder;
        return fallback ?? (item.IsExpanded ? FallbackFolderOpenIcon : FallbackFolderIcon);
    }

    private static string ResolveFile(FileSystemItem item)
    {
        var map = Map.Value;
        string nameLower = item.Name.ToLowerInvariant();

        if (map.FileNames.TryGetValue(nameLower, out var byName))
        {
            return byName;
        }

        int firstDot = nameLower.IndexOf('.');
        if (firstDot >= 0 && firstDot < nameLower.Length - 1)
        {
            string compoundExt = nameLower[(firstDot + 1)..];
            if (map.FileExtensions.TryGetValue(compoundExt, out var byCompoundExt))
            {
                return byCompoundExt;
            }
        }

        string finalExt = Path.GetExtension(nameLower).TrimStart('.');
        if (!string.IsNullOrEmpty(finalExt) && map.FileExtensions.TryGetValue(finalExt, out var byExt))
        {
            return byExt;
        }

        return map.DefaultFile ?? FallbackFileIcon;
    }

    private static IconMap LoadMap()
    {
        var uri = new Uri("pack://application:,,,/Resources/icon-map.json");
        var resourceStream = Application.GetResourceStream(uri)
            ?? throw new FileNotFoundException("icon-map.json resource not found");

        using var stream = resourceStream.Stream;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<IconMap>(stream, options) ?? new IconMap();
    }
}
