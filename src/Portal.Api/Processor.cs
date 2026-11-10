namespace Portal.Api;

public sealed class Processor(Store store, ILogger<Processor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claim = store.Take(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (claim is null) { await Task.Delay(500, stoppingToken); continue; }
                await Task.Delay(150, stoppingToken);
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var success = store.InvokeProvider(claim, now);
                store.Finish(claim, success, now);
                logger.LogInformation("Request {Id}: attempt {Attempt}, success {Success}", claim.Request.Id, claim.Request.Attempts, success);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Processing failed; lease permits recovery");
                try { await Task.Delay(1000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
