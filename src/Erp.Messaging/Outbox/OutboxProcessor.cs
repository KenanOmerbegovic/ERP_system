using System.Text.Json;
using Erp.SharedKernel.Messaging;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Messaging.Outbox;

/// <summary>
/// Delivers the messages in one module's outbox, oldest first. Delivery is at least once:
/// a message is marked processed only after all its handlers succeed.
/// Assumes a single API instance; see docs/open-decisions.md.
/// </summary>
public sealed partial class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollInterval);
        do
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken) == options.Value.BatchSize)
                {
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogBatchFailed(logger, exception, typeof(TDbContext).Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Processes one batch and returns how many messages it contained.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<TDbContext>();
        var types = services.GetRequiredService<IntegrationEventTypes>();
        var dispatcher = services.GetRequiredService<IntegrationEventDispatcher>();
        var clock = services.GetRequiredService<IClock>();
        var maxAttempts = options.Value.MaxAttempts;

        var messages = await db.Set<OutboxMessage>()
            .Where(message => message.ProcessedAt == null && message.Attempts < maxAttempts)
            .OrderBy(message => message.OccurredAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = types.Find(message.Type);
                if (eventType is not null)
                {
                    var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, eventType, OutboxJson.Options)!;
                    await dispatcher.DispatchAsync(integrationEvent, cancellationToken);
                }

                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogDeliveryFailed(logger, exception, message.Id, message.Type);
                message.MarkFailed(exception.ToString());
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox of {DbContext} could not be processed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, string dbContext);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({Type}) could not be delivered")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, Guid messageId, string type);
}
