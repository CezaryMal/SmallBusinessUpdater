namespace SmallBusinessUpdater.Services;

public class UpdateCheckResult
{
	public bool Success { get; set; }

	public bool UpdateAvailable { get; set; }

	public string Message { get; set; } = "";

	public string? InstallerPath { get; set; }

	public string? InstallerHash { get; set; }

	public string? ProductVersion { get; set; }

	public string? FileVersion { get; set; }

	public string? SignerName { get; set; }

	public int BuildNumber { get; set; }

	public int InstalledBuild { get; set; }
}
