using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmallBusinessUpdater.Services;

internal static class SelfUpdateArchiveService
{
    public static void Clean(string directory, string? protectedPath = null)
    {
        try
        {
            var installers = Directory.EnumerateFiles(directory)
                .Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                .Select(path => new
                {
                    Path = path,
                    Match = Regex.Match(Path.GetFileName(path),
                        @"^SmallBusinessUpdater_Setup_v(\d+\.\d+\.\d+\.\d+)\.exe$",
                        RegexOptions.IgnoreCase)
                })
                .Where(item => item.Match.Success && Version.TryParse(item.Match.Groups[1].Value, out _))
                .Select(item => new { item.Path, Version = Version.Parse(item.Match.Groups[1].Value) })
                .OrderByDescending(item => item.Version)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var keep = installers.Take(3).Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (protectedPath != null)
                keep.Add(Path.GetFullPath(protectedPath));

            foreach (var item in installers.Where(item => !keep.Contains(item.Path)))
            {
                try
                {
                    File.Delete(item.Path);
                    LogService.Write("self-update.log", "Usunięto stary instalator: " + Path.GetFileName(item.Path));
                }
                catch (IOException ex) { LogService.Write("self-update.log", ex.Message); }
                catch (UnauthorizedAccessException ex) { LogService.Write("self-update.log", ex.Message); }
            }
        }
        catch (Exception ex) { LogService.Write("self-update.log", ex.Message); }
    }
}
