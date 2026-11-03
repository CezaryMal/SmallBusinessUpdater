using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace SmallBusinessUpdater.Services;

public static class AutoStartService
{
	private const string RunRegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "SmallBusinessUpdater";

	public static void Apply(bool enabled)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
		if (registryKey == null)
		{
			throw new InvalidOperationException("Nie udało się otworzyć ustawień autostartu Windows.");
		}
		if (!enabled)
		{
			registryKey.DeleteValue("SmallBusinessUpdater", throwOnMissingValue: false);
			return;
		}
		string text = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Process.GetCurrentProcess().MainModule?.FileName;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException("Nie udało się ustalić ścieżki programu.");
		}
		string value = "\"" + text + "\" --startup";
		registryKey.SetValue("SmallBusinessUpdater", value, RegistryValueKind.String);
	}

	public static bool IsEnabled()
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
		if (registryKey == null)
		{
			return false;
		}
		return registryKey.GetValue("SmallBusinessUpdater") is string value && !string.IsNullOrWhiteSpace(value);
	}
}
