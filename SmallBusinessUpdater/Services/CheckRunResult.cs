namespace SmallBusinessUpdater.Services;

public enum CheckRunResult
{
	Completed,
	ConfigurationInvalid,
	UpdatePeriodExpired,
	AlreadyRunning,
	Failed
}
