using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SmallBusinessUpdater.Services;

public sealed class SingleInstanceService : IDisposable
{
	private const string MutexName = "Local\\SmallBusinessUpdater.MainGui";

	private const int SwRestore = 9;

	private readonly Mutex _mutex;

	private bool _ownsMutex;

	public bool Acquired => _ownsMutex;

	private SingleInstanceService(Mutex mutex, bool ownsMutex)
	{
		_mutex = mutex;
		_ownsMutex = ownsMutex;
	}

	public static SingleInstanceService TryAcquire()
	{
		Mutex mutex = new Mutex(initiallyOwned: false, "Local\\SmallBusinessUpdater.MainGui");
		bool ownsMutex;
		try
		{
			ownsMutex = mutex.WaitOne(TimeSpan.Zero);
		}
		catch (AbandonedMutexException)
		{
			ownsMutex = true;
		}
		return new SingleInstanceService(mutex, ownsMutex);
	}

	public static void ActivateExistingWindow()
	{
		Process currentProcess = Process.GetCurrentProcess();
		Process[] processesByName = Process.GetProcessesByName(currentProcess.ProcessName);
		foreach (Process process in processesByName)
		{
			try
			{
				if (process.Id != currentProcess.Id)
				{
					process.Refresh();
					nint mainWindowHandle = process.MainWindowHandle;
					if (mainWindowHandle != IntPtr.Zero)
					{
						ShowWindow(mainWindowHandle, 9);
						SetForegroundWindow(mainWindowHandle);
						break;
					}
				}
			}
			catch
			{
			}
		}
	}

	public void Dispose()
	{
		if (_ownsMutex)
		{
			try
			{
				_mutex.ReleaseMutex();
			}
			catch
			{
			}
			_ownsMutex = false;
		}
		_mutex.Dispose();
	}

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
