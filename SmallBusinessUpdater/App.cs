using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SmallBusinessUpdater.Services;

namespace SmallBusinessUpdater;

public class App : Application
{
	private SingleInstanceService? _guiInstance;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		AppPaths.EnsureDirectories();
		string[] args = e.Args;
		if (args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
		{
			await RunStartupModeAsync();
		}
		else if (args.Contains("--check", StringComparer.OrdinalIgnoreCase))
		{
			await RunCheckModeAsync();
		}
		else if (!AcquireGuiInstance())
		{
			SingleInstanceService.ActivateExistingWindow();
			Shutdown();
		}
		else
		{
			OpenMainWindow();
		}
	}

	private bool AcquireGuiInstance()
	{
		if (_guiInstance != null)
		{
			return true;
		}
		SingleInstanceService singleInstanceService = SingleInstanceService.TryAcquire();
		if (!singleInstanceService.Acquired)
		{
			singleInstanceService.Dispose();
			return false;
		}
		_guiInstance = singleInstanceService;
		return true;
	}

	private void OpenMainWindow()
	{
		if (_guiInstance == null && !AcquireGuiInstance())
		{
			SingleInstanceService.ActivateExistingWindow();
			Shutdown();
		}
		else if (MainWindow is MainWindow mainWindow)
		{
			if (!mainWindow.IsVisible)
			{
				mainWindow.Show();
			}
			if (mainWindow.WindowState == WindowState.Minimized)
			{
				mainWindow.WindowState = WindowState.Normal;
			}
			mainWindow.Activate();
		}
		else
		{
			ShutdownMode = ShutdownMode.OnLastWindowClose;
			MainWindow mainWindow2 = (MainWindow)(MainWindow = new MainWindow());
			mainWindow2.Show();
			mainWindow2.Activate();
		}
	}

	private async Task RunStartupModeAsync()
	{
		ShutdownMode = ShutdownMode.OnExplicitShutdown;
		StartupCheckWindow checkWindow = null;
		try
		{
			checkWindow = new StartupCheckWindow();
			checkWindow.Show();
			checkWindow.Activate();
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)(() =>
			{
			}), (DispatcherPriority)4);
			StartupRunResult startupRunResult = await StartupService.RunAsync(() =>
			{
				CloseStartupCheckWindow(checkWindow);
				checkWindow = null;
			});
			CloseStartupCheckWindow(checkWindow);
			checkWindow = null;
			switch (startupRunResult)
			{
			case StartupRunResult.OpenGui:
				OpenMainWindow();
				return;
			case StartupRunResult.UpdatePeriodExpired:
				ShowExpiredWindow();
				return;
			case StartupRunResult.UpdateAlreadyRunning:
				Shutdown();
				return;
			}
		}
		catch
		{
			CloseStartupCheckWindow(checkWindow);
		}
		Shutdown();
	}

	private async Task RunCheckModeAsync()
	{
		ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			switch (await CheckService.RunAsync())
			{
			case CheckRunResult.ConfigurationInvalid:
				OpenMainWindow();
				return;
			case CheckRunResult.UpdatePeriodExpired:
				ShowExpiredWindow();
				return;
			}
		}
		catch
		{
		}
		Shutdown();
	}

	private void CloseStartupCheckWindow(StartupCheckWindow? window)
	{
		if (window == null)
		{
			return;
		}
		try
		{
			if (!((DispatcherObject)this).Dispatcher.CheckAccess())
			{
				((DispatcherObject)this).Dispatcher.Invoke((Action)(() =>
				{
					CloseStartupCheckWindow(window);
				}));
			}
			else
			{
				window.Close();
			}
		}
		catch
		{
		}
	}

	private void ShowExpiredWindow()
	{
		if (new ExpiredUpdateWindow(ConfigService.Load().UpdateEndDate).ShowDialog() == true)
		{
			OpenMainWindow();
		}
		else
		{
			Shutdown();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_guiInstance?.Dispose();
		_guiInstance = null;
		base.OnExit(e);
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public static void Main()
	{
		new App().Run();
	}
}
