using System;

namespace SmallBusinessUpdater.Models;

public class UpdateState
{
	public int InstalledBuild { get; set; }

	public string InstalledProductVersion { get; set; } = "";

	public string InstallerHash { get; set; } = "";

	public DateTime? InstalledAt { get; set; }
}
