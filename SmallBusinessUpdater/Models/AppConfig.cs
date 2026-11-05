using System;

namespace SmallBusinessUpdater.Models;

public class AppConfig
{
	public string SymplexId { get; set; } = "";

	public string Nip { get; set; } = "";

	public DateTime UpdateEndDate { get; set; } = DateTime.Today;

	public string SmallBusinessPath { get; set; } = "C:\\SB4\\ksiegaw.exe";

	public bool DarkTheme { get; set; }

	public bool AutoStart { get; set; }

	public bool LaunchAfterUpdate { get; set; } = true;
}

