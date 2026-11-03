using System;
using System.Threading;

namespace SmallBusinessUpdater.Services;

public sealed class UpdateLockService : IDisposable
{
	private const string MutexName = "Local\\SmallBusinessUpdater.UpdateOperation";

	private readonly Mutex _mutex;

	private bool _ownsMutex;

	public bool Acquired => _ownsMutex;

	private UpdateLockService(Mutex mutex, bool ownsMutex)
	{
		_mutex = mutex;
		_ownsMutex = ownsMutex;
	}

	public static UpdateLockService TryAcquire()
	{
		Mutex mutex = new Mutex(initiallyOwned: false, "Local\\SmallBusinessUpdater.UpdateOperation");
		bool flag = false;
		try
		{
			flag = mutex.WaitOne(TimeSpan.Zero);
		}
		catch (AbandonedMutexException)
		{
			flag = true;
		}
		return new UpdateLockService(mutex, flag);
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
}
