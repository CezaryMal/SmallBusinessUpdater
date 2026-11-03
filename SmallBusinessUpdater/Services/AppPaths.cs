using System;
using System.IO;

namespace SmallBusinessUpdater.Services;

public static class AppPaths
{
	public static string AppDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmallBusinessUpdater");

	public static string LocalDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmallBusinessUpdater");

	public static string ConfigPath => Path.Combine(AppDataDirectory, "config.json");

	public static string LogsDirectory => Path.Combine(LocalDataDirectory, "Logs");

	public static string DownloadsDirectory => Path.Combine(LocalDataDirectory, "Downloads");

	public static void EnsureDirectories()
	{
		Directory.CreateDirectory(AppDataDirectory);
		Directory.CreateDirectory(LocalDataDirectory);
		Directory.CreateDirectory(LogsDirectory);
		Directory.CreateDirectory(DownloadsDirectory);
	}
}
