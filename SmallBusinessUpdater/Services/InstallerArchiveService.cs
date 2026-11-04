using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmallBusinessUpdater.Services;

internal static class InstallerArchiveService
{
    public static void Clean(string protectedPath)
    {
        try
        {
            var candidates = Directory.EnumerateFiles(AppPaths.DownloadsDirectory)
                .Where(path => !new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
                .Select(path => new { Path = path, Match = Regex.Match(Path.GetFileName(path), @"^inst_sb(\d+).*\.exe$", RegexOptions.IgnoreCase) })
                .Where(item => item.Match.Success && int.TryParse(item.Match.Groups[1].Value, out _))
                .Select(item => new { item.Path, Build = int.Parse(item.Match.Groups[1].Value), Date = File.GetLastWriteTimeUtc(item.Path) })
                .OrderByDescending(item => item.Build).ThenByDescending(item => item.Date).ToList();
            var keep = candidates.GroupBy(item => item.Build).Take(3)
                .Select(group => group.First().Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            keep.Add(Path.GetFullPath(protectedPath));
            foreach (var item in candidates.Where(item => !keep.Contains(item.Path)))
            {
                try { File.Delete(item.Path); }
                catch (IOException ex) { LogService.Write("cleanup.log", ex.Message); }
                catch (UnauthorizedAccessException ex) { LogService.Write("cleanup.log", ex.Message); }
            }
        }
        catch (Exception ex) { LogService.Write("cleanup.log", ex.Message); }
    }
}
