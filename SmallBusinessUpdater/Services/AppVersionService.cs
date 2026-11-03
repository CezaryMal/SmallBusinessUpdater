using System;
using System.Reflection;

namespace SmallBusinessUpdater.Services;

public static class AppVersionService
{
	public static string Version
	{
		get
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			if ((object)version == null)
			{
				return "0.0.0";
			}
			return $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}

	public static string DisplayVersion => "v" + Version;
}
