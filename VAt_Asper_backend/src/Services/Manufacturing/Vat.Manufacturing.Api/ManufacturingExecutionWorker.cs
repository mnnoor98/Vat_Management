using Microsoft.Extensions.Options;
using Vat.Application;
using Vat.Infrastructure.Messaging;

public sealed class ManufacturingExecutionWorker(IServiceScopeFactory scopes,RabbitMqTransport transport,IOptions<MessagingSettings> options,ILogger<ManufacturingExecutionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Value.Enabled)return;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await transport.ConsumeManufacturing(async(message,token)=>
                {
                    await using var scope=scopes.CreateAsyncScope();
                    var result=await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(message,token);
                    if(result.Status is not("Processed" or "Ignored" or "Duplicate" or "Quarantined"))throw new InvalidOperationException("Manufacturing inbox did not commit delivery.");
                },ct);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception){logger.LogWarning("Manufacturing execution consumer disconnected; unacknowledged delivery will be retried.");}
            await Task.Delay(TimeSpan.FromSeconds(5),ct);
        }
    }
}
