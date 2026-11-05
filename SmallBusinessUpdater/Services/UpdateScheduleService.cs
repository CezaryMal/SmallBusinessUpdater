using System;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

internal static class UpdateScheduleService
{
    public static bool IsDue(AppConfig config) =>
        IsDue(config.CheckIntervalDays, CheckHistoryService.Load().LastCompletedAt, DateTimeOffset.Now);

    internal static bool IsDue(int intervalDays, DateTimeOffset? completedAt, DateTimeOffset now)
    {
        if (!completedAt.HasValue || completedAt.Value > now) return true;
        int days = Math.Clamp(intervalDays, 1, 365);
        return now.LocalDateTime.Date >= completedAt.Value.LocalDateTime.Date.AddDays(days);
    }
}
