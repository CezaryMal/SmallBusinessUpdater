using System;
using System.IO;
using System.Text.Json;

namespace SmallBusinessUpdater.Services;

public sealed class CheckHistory
{
    public DateTimeOffset? CheckedAt { get; set; }
    public string Result { get; set; } = "";
}

internal static class CheckHistoryService
{
    private static string HistoryPath => Path.Combine(AppPaths.AppDataDirectory, "last-check.json");

    public static CheckHistory Load()
    {
        try
        {
            return File.Exists(HistoryPath)
                ? JsonSerializer.Deserialize<CheckHistory>(File.ReadAllText(HistoryPath)) ?? new CheckHistory()
                : new CheckHistory();
        }
        catch (Exception ex)
        {
            LogService.Write("check.log", "Odczyt historii: " + ex.Message);
            return new CheckHistory();
        }
    }

    public static void Save(string result)
    {
        string temporary = HistoryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            AppPaths.EnsureDirectories();
            File.WriteAllText(temporary, JsonSerializer.Serialize(new CheckHistory
            {
                CheckedAt = DateTimeOffset.Now,
                Result = result
            }));
            File.Move(temporary, HistoryPath, true);
        }
        catch (Exception ex)
        {
            LogService.Write("check.log", "Zapis historii: " + ex.Message);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
