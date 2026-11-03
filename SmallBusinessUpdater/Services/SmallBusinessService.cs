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
}
