using System;
using System.IO;
using System.Text.Json;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

public static class VersionStateService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static string StatePath => Path.Combine(AppPaths.AppDataDirectory, "state.json");

	public static UpdateState Load()
	{
		AppPaths.EnsureDirectories();
		if (!File.Exists(StatePath))
		{
			return new UpdateState();
		}
		try
		{
			return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(StatePath)) ?? new UpdateState();
		}
		catch
		{
			return new UpdateState();
		}
	}

	public static void SaveInstalledBuild(int build, string productVersion, string installerHash)
	{
		AppPaths.EnsureDirectories();
		string contents = JsonSerializer.Serialize(new UpdateState
		{
			InstalledBuild = build,
			InstalledProductVersion = productVersion,
			InstallerHash = installerHash,
			InstalledAt = DateTime.Now
		}, JsonOptions);
		File.WriteAllText(StatePath, contents);
	}
}
