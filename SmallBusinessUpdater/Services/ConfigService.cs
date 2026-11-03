using System.IO;
using System.Text.Json;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

public static class ConfigService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static AppConfig Load()
	{
		AppPaths.EnsureDirectories();
		if (!File.Exists(AppPaths.ConfigPath))
		{
			return new AppConfig();
		}
		try
		{
			return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigPath)) ?? new AppConfig();
		}
		catch
		{
			return new AppConfig();
		}
	}

	public static void Save(AppConfig config)
	{
		AppPaths.EnsureDirectories();
		string contents = JsonSerializer.Serialize(config, JsonOptions);
		File.WriteAllText(AppPaths.ConfigPath, contents);
	}
}
