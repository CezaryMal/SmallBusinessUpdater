using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmallBusinessUpdater.Services;

internal static class NetworkRetryService
{
    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> request,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            try
            {
                return await request(timeout.Token);
            }
            catch (Exception ex) when (attempt < 2 && !cancellationToken.IsCancellationRequested && IsTransient(ex))
            {
                int seconds = attempt == 0 ? 5 : 15;
                string message = $"Problem z połączeniem. Kolejna próba ({attempt + 2} z 3) za {seconds} s…";
                LogService.Write("network.log", message + " " + ex.Message);
                progress?.Report(message);
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception error) =>
        error is OperationCanceledException ||
        error is HttpRequestException http &&
        (!http.StatusCode.HasValue || http.StatusCode == HttpStatusCode.RequestTimeout ||
         (int)http.StatusCode.Value == 429 || (int)http.StatusCode.Value >= 500);
}
