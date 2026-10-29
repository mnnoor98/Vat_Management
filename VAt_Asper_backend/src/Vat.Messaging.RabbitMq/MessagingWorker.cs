using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vat.Messaging;

namespace Vat.Infrastructure.Messaging;

public sealed class MessagingWorker(IServiceScopeFactory scopes, RabbitMqTransport transport,
    IOptions<MessagingSettings> options, ILogger<MessagingWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => options.Value.Enabled
        ? Task.WhenAll(Dispatch(stoppingToken), Consume(stoppingToken)) : Task.CompletedTask;

    private async Task Dispatch(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchBatch(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Outbox dispatch deferred; database or broker operation unavailable."); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), ct);
        }
    }

    private async Task Consume(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await transport.Consume(async (message, token) =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<InboxDeliveryHandler>().Process(message, token);
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Reporting consumer disconnected; unacknowledged deliveries will be retried."); }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
