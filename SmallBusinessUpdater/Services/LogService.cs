using System;
using System.IO;

namespace SmallBusinessUpdater.Services;

public static class LogService
{
	private const long MaxLogSizeBytes = 2097152L;

	private const int MaxArchivedFiles = 3;

	public static void Write(string fileName, string message)
	{
		try
		{
			AppPaths.EnsureDirectories();
			string path = Path.Combine(AppPaths.LogsDirectory, fileName);
			RotateIfNeeded(path);
			string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " + message;
			File.AppendAllText(path, text + Environment.NewLine);
		}
		catch
		{
		}
	}

	private static void RotateIfNeeded(string path)
	{
		if (!File.Exists(path) || new FileInfo(path).Length < 2097152)
		{
			return;
		}
		string path2 = path + "." + 3;
		if (File.Exists(path2))
		{
			File.Delete(path2);
		}
		for (int num = 2; num >= 1; num--)
		{
			string text = path + "." + num;
			string destFileName = path + "." + (num + 1);
			if (File.Exists(text))
			{
				File.Move(text, destFileName, overwrite: true);
			}
		}
		File.Move(path, path + ".1", overwrite: true);
	}
}
