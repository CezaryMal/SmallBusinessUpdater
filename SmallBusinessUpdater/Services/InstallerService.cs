using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmallBusinessUpdater.Services;

public static class InstallerService
{
	private class InstallerWindowInfo
	{
		public int ProcessId { get; set; }

		public string ProcessName { get; set; } = "";

		public nint WindowHandle { get; set; }

		public string Title { get; set; } = "";
	}

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	private const int SwRestore = 9;

	private const byte VkMenu = 18;

	private const uint KeyeventfKeyup = 2u;

	private const string InstallerTitlePrefix = "Instalator SMALL BUSINESS";

	public static async Task<InstallResult> InstallAsync(string installerPath, string smallBusinessPath, bool launchAfterUpdate, IProgress<string>? progress = null)
	{
		_ = 14;
		try
		{
			if (!File.Exists(installerPath))
			{
				return Fail("Nie znaleziono pobranego instalatora.");
			}
			if (!File.Exists(smallBusinessPath))
			{
				return Fail("Nie znaleziono pliku ksiegaw.exe.");
			}
            if (SmallBusinessService.IsRunning(smallBusinessPath))
            {
                return Fail("Small Business jest otwarty. Zapisz pracę, zamknij program i ponownie sprawdź aktualizacje.");
            }
            AppPaths.EnsureDirectories();
			Log("");
			Log("============================================");
			Log("NOWA INSTALACJA");
			Log("Installer: " + installerPath);
			Log("Small Business: " + smallBusinessPath);
			progress?.Report("Aktualizowanie Small Business...");
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = installerPath,
				WorkingDirectory = (Path.GetDirectoryName(installerPath) ?? ""),
				UseShellExecute = true
			});
			if (process == null)
			{
				return Fail("Nie udało się uruchomić instalatora.");
			}
			Log($"Wrapper PID: {process.Id}");
			nint num = await WaitForAnyVisibleWindowAsync(process.Id, TimeSpan.FromMinutes(1L));
			if (num == IntPtr.Zero)
			{
				return Fail("Nie udało się znaleźć pierwszego okna instalatora.");
			}
			Log($"Wrapper HWND: 0x{((IntPtr)num).ToInt64():X}");
			ActivateWindow(num);
			await Task.Delay(400);
			SendAltKey('I');
			InstallerWindowInfo installer = await WaitForSmallBusinessInstallerAsync(TimeSpan.FromMinutes(5L));
			if (installer == null)
			{
				return Fail("Nie pojawił się właściwy instalator Small Business.");
			}
			Log($"Installer PID: {installer.ProcessId}");
			Log($"Installer HWND: 0x{((IntPtr)installer.WindowHandle).ToInt64():X}");
			Log("Installer TITLE: " + installer.Title);
			await Task.Delay(500);
			await SendInstallerShortcutAsync(installer.ProcessId, 'D', 700);
			await SendInstallerShortcutAsync(installer.ProcessId, 'A', 700);
			await SendInstallerShortcutAsync(installer.ProcessId, 'D', 700);
			await SendInstallerShortcutAsync(installer.ProcessId, 'D', 700);
			await SendInstallerShortcutAsync(installer.ProcessId, 'D', 1000);
			HashSet<nint> windowsBeforeInstall = (from x in FindAllInstallerWindows()
				select x.WindowHandle).ToHashSet();
			Log("Okna przed instalacją: " + string.Join(", ", windowsBeforeInstall.Select((nint x) => $"0x{((IntPtr)x).ToInt64():X}")));
			await SendInstallerShortcutAsync(installer.ProcessId, 'I', 500);
			progress?.Report("Aktualizowanie Small Business...");
			InstallerWindowInfo installerWindowInfo = await WaitForNewInstallerWindowAsync(windowsBeforeInstall, TimeSpan.FromMinutes(10L));
			if (installerWindowInfo == null)
			{
				Log("Nie wykryto nowego HWND. Uruchamiam tryb awaryjny.");
				if (!(await WaitUntilInstallationIsQuietFallbackAsync(TimeSpan.FromMinutes(10L))))
				{
					return Fail("Instalator nie zakończył pracy w oczekiwanym czasie.");
				}
			}
			else
			{
				Log($"Wykryto końcowe okno: PID={installerWindowInfo.ProcessId}, HWND=0x{((IntPtr)installerWindowInfo.WindowHandle).ToInt64():X}, TITLE={installerWindowInfo.Title}");
			}
			progress?.Report("Kończenie aktualizacji...");
			if (!(await CloseGotoweWindowAsync(TimeSpan.FromSeconds(20L))))
			{
				return Fail("Nie udało się zamknąć ekranu Gotowe.");
			}
			Log("Ekran Gotowe został rzeczywiście zamknięty.");
			await Task.Delay(150);
			if (launchAfterUpdate)
			{
				progress?.Report("Uruchamianie Small Business...");
				await Task.Delay(150);
				StartSmallBusiness(smallBusinessPath);
			}
			else
			{
				progress?.Report("Aktualizacja zakończona.");
			}
			Log("Instalacja zakończona poprawnie.");
			return new InstallResult
			{
				Success = true,
				Message = "Aktualizacja została zainstalowana."
			};
		}
		catch (Exception ex)
		{
			Log($"WYJĄTEK: {ex}");
			return Fail(ex.Message);
		}
	}

	private static async Task<InstallerWindowInfo?> WaitForNewInstallerWindowAsync(HashSet<nint> oldWindows, TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		await Task.Delay(500);
		while (DateTime.Now < end)
		{
			List<InstallerWindowInfo> list = FindAllInstallerWindows();
			foreach (InstallerWindowInfo window in list)
			{
				if (!oldWindows.Contains(window.WindowHandle))
				{
					await Task.Delay(200);
					InstallerWindowInfo installerWindowInfo = FindAllInstallerWindows().FirstOrDefault((InstallerWindowInfo x) => x.WindowHandle == window.WindowHandle);
					if (installerWindowInfo != null)
					{
						return installerWindowInfo;
					}
				}
			}
			await Task.Delay(100);
		}
		return null;
	}

	private static async Task<InstallerWindowInfo?> WaitForSmallBusinessInstallerAsync(TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		while (DateTime.Now < end)
		{
			Process[] processes = Process.GetProcesses();
			foreach (Process process in processes)
			{
				try
				{
					if (process.ProcessName.Equals("ksiega", StringComparison.OrdinalIgnoreCase))
					{
						InstallerWindowInfo installerWindowInfo = FindInstallerWindowForProcess(process.Id);
						if (installerWindowInfo != null)
						{
							return installerWindowInfo;
						}
					}
				}
				catch
				{
				}
			}
			await Task.Delay(150);
		}
		return null;
	}

	private static async Task SendInstallerShortcutAsync(int processId, char key, int delayAfterMs)
	{
		ActivateWindow(((await WaitForInstallerWindowAsync(processId, TimeSpan.FromSeconds(30L))) ?? throw new InvalidOperationException("Nie znaleziono okna Instalator SMALL BUSINESS.")).WindowHandle);
		await Task.Delay(150);
		SendAltKey(key);
		try
		{
			Process.GetProcessById(processId).WaitForInputIdle(10000);
		}
		catch
		{
		}
		await Task.Delay(delayAfterMs);
	}

	private static async Task<InstallerWindowInfo?> WaitForInstallerWindowAsync(int processId, TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		while (DateTime.Now < end)
		{
			InstallerWindowInfo installerWindowInfo = FindInstallerWindowForProcess(processId);
			if (installerWindowInfo != null)
			{
				return installerWindowInfo;
			}
			await Task.Delay(100);
		}
		return null;
	}

	private static async Task<bool> WaitUntilInstallationIsQuietFallbackAsync(TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		Dictionary<int, TimeSpan> previousCpu = new Dictionary<int, TimeSpan>();
		DateTime? quietSince = null;
		while (DateTime.Now < end)
		{
			List<Process> ksiegaProcesses = GetKsiegaProcesses();
			if (ksiegaProcesses.Count == 0)
			{
				return true;
			}
			bool flag = true;
			foreach (Process item in ksiegaProcesses)
			{
				try
				{
					item.Refresh();
					TimeSpan totalProcessorTime = item.TotalProcessorTime;
					if (previousCpu.TryGetValue(item.Id, out var value))
					{
						if ((totalProcessorTime - value).TotalMilliseconds >= 40.0)
						{
							flag = false;
						}
					}
					else
					{
						flag = false;
					}
					previousCpu[item.Id] = totalProcessorTime;
				}
				catch
				{
				}
			}
			if (flag && FindAllInstallerWindows().Count > 0)
			{
				quietSince.GetValueOrDefault();
				if (!quietSince.HasValue)
				{
					DateTime now = DateTime.Now;
					quietSince = now;
				}
				if ((DateTime.Now - quietSince.Value).TotalSeconds >= 1.0)
				{
					return true;
				}
			}
			else
			{
				quietSince = null;
			}
			await Task.Delay(250);
		}
		return false;
	}

	private static async Task<bool> CloseGotoweWindowAsync(TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		int attempt = 0;
		int noWindowChecks = 0;
		while (DateTime.Now < end)
		{
			attempt++;
			List<InstallerWindowInfo> list = FindAllInstallerWindows();
			Log($"Próba Gotowe #{attempt}. Okna: {list.Count}");
			if (list.Count == 0)
			{
				noWindowChecks++;
				if (noWindowChecks >= 2)
				{
					Log("Brak okna instalatora - Gotowe zamknięte.");
					return true;
				}
				await Task.Delay(100);
				continue;
			}
			noWindowChecks = 0;
			foreach (InstallerWindowInfo item in list)
			{
				Log($"GOTOWE CANDIDATE: PID={item.ProcessId}, PROCESS={item.ProcessName}, HWND=0x{((IntPtr)item.WindowHandle).ToInt64():X}, TITLE={item.Title}");
				ActivateWindow(item.WindowHandle);
				await Task.Delay(100);
				SendAltKey('G');
				await Task.Delay(200);
			}
			await Task.Delay(100);
		}
		return false;
	}

	private static bool IsInstallerTitle(string title)
	{
		return title.StartsWith("Instalator SMALL BUSINESS", StringComparison.OrdinalIgnoreCase);
	}

	private static List<InstallerWindowInfo> FindAllInstallerWindows()
	{
		List<InstallerWindowInfo> result = new List<InstallerWindowInfo>();
		EnumWindows((nint handle, nint _) =>
		{
			if (!IsWindowVisible(handle))
			{
				return true;
			}
			string windowTitle = GetWindowTitle(handle);
			if (!IsInstallerTitle(windowTitle))
			{
				return true;
			}
			GetWindowThreadProcessId(handle, out var processId);
			string processName = "";
			try
			{
				processName = Process.GetProcessById((int)processId).ProcessName;
			}
			catch
			{
			}
			result.Add(new InstallerWindowInfo
			{
				ProcessId = (int)processId,
				ProcessName = processName,
				WindowHandle = handle,
				Title = windowTitle
			});
			return true;
		}, IntPtr.Zero);
		return result;
	}

	private static InstallerWindowInfo? FindInstallerWindowForProcess(int processId)
	{
		InstallerWindowInfo result = null;
		EnumWindows((nint handle, nint _) =>
		{
			if (!IsWindowVisible(handle))
			{
				return true;
			}
			GetWindowThreadProcessId(handle, out var processId2);
			if (processId2 != processId)
			{
				return true;
			}
			string windowTitle = GetWindowTitle(handle);
			if (!IsInstallerTitle(windowTitle))
			{
				return true;
			}
			string processName = "";
			try
			{
				processName = Process.GetProcessById(processId).ProcessName;
			}
			catch
			{
			}
			result = new InstallerWindowInfo
			{
				ProcessId = processId,
				ProcessName = processName,
				WindowHandle = handle,
				Title = windowTitle
			};
			return false;
		}, IntPtr.Zero);
		return result;
	}

	private static List<Process> GetKsiegaProcesses()
	{
		List<Process> list = new List<Process>();
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (process.ProcessName.Equals("ksiega", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(process);
				}
			}
			catch
			{
			}
		}
		return list;
	}

	private static async Task<nint> WaitForAnyVisibleWindowAsync(int processId, TimeSpan timeout)
	{
		DateTime end = DateTime.Now + timeout;
		while (DateTime.Now < end)
		{
			nint result = IntPtr.Zero;
			EnumWindows((nint handle, nint _) =>
			{
				if (!IsWindowVisible(handle))
				{
					return true;
				}
				GetWindowThreadProcessId(handle, out var processId2);
				if (processId2 == processId)
				{
					result = handle;
					return false;
				}
				return true;
			}, IntPtr.Zero);
			if (result != IntPtr.Zero)
			{
				return result;
			}
			try
			{
				if (Process.GetProcessById(processId).HasExited)
				{
					return IntPtr.Zero;
				}
			}
			catch
			{
				return IntPtr.Zero;
			}
			await Task.Delay(100);
		}
		return IntPtr.Zero;
	}

	private static string GetWindowTitle(nint handle)
	{
		int windowTextLength = GetWindowTextLength(handle);
		if (windowTextLength <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
		GetWindowText(handle, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	private static void StartSmallBusiness(string smallBusinessPath)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = smallBusinessPath,
			WorkingDirectory = (Path.GetDirectoryName(smallBusinessPath) ?? ""),
			UseShellExecute = true
		});
	}

	private static void ActivateWindow(nint window)
	{
		if (window != IntPtr.Zero)
		{
			ShowWindow(window, 9);
			SetForegroundWindow(window);
			Thread.Sleep(60);
			SetForegroundWindow(window);
		}
	}

	private static void SendAltKey(char character)
	{
		byte bVk = (byte)(VkKeyScan(character) & 0xFF);
		keybd_event(18, 0, 0u, UIntPtr.Zero);
		Thread.Sleep(40);
		keybd_event(bVk, 0, 0u, UIntPtr.Zero);
		Thread.Sleep(40);
		keybd_event(bVk, 0, 2u, UIntPtr.Zero);
		Thread.Sleep(40);
		keybd_event(18, 0, 2u, UIntPtr.Zero);
	}

	private static InstallResult Fail(string message)
	{
		Log("BŁĄD: " + message);
		return new InstallResult
		{
			Success = false,
			Message = message
		};
	}

	private static void Log(string message)
	{
		LogService.Write("installer.log", message);
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern short VkKeyScan(char ch);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);
}

