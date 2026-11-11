// Documentación técnica: Jorge Gomez.
namespace Portal.Api;

/// <summary>Procesa fuera del ciclo HTTP y permite recuperar claims abandonados al expirar.</summary>
public sealed class Processor(Store store, ILogger<Processor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Cede el inicio al host para que el bucle no retenga el arranque de la API.
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claim = store.Take(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (claim is null) { await Task.Delay(500, stoppingToken); continue; }
                await Task.Delay(150, stoppingToken);
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // El recibo se persiste antes del estado para reconciliar una caída intermedia.
                var success = store.InvokeProvider(claim, now);
                store.Finish(claim, success, now);
                logger.LogInformation("Request {Id}: attempt {Attempt}, success {Success}", claim.Request.Id, claim.Request.Attempts, success);
            }
            // Una detención normal deja el claim disponible para recuperación al expirar su lease.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Processing failed; lease permits recovery");
                try { await Task.Delay(1000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
