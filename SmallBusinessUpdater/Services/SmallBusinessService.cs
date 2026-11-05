using System;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace SmallBusinessUpdater.Services;

public static class SmallBusinessService
{
	public static bool Start(string path)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}
			if (!File.Exists(path))
			{
				return false;
			}
            if (IsRunning(path)) return true;
			Process.Start(new ProcessStartInfo
			{
				FileName = path,
				WorkingDirectory = (Path.GetDirectoryName(path) ?? ""),
				UseShellExecute = true
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

    public static bool IsRunning(string path)
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name)) return false;
            var processes = Process.GetProcessesByName(name);
            try
            {
                return processes.Any(process => !process.HasExited);
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogService.Write("installer.log", "Nie udało się sprawdzić uruchomionych procesów: " + ex.Message);
            return true;
        }
    }
}

