using System.Text.Json;
using Erp.SharedKernel.Messaging;

namespace Erp.Messaging.Outbox;

/// <summary>An integration event waiting in a module's outbox, saved in the same transaction as the module's data.</summary>
public sealed class OutboxMessage
{
    private OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The event's CLR full name; see <see cref="IntegrationEventTypes"/>.</summary>
    public string Type { get; private init; }

    /// <summary>The event as JSON.</summary>
    public string Content { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var type = integrationEvent.GetType();
        return new OutboxMessage(
            integrationEvent.Id,
            IntegrationEventTypes.NameOf(type),
            JsonSerializer.Serialize(integrationEvent, type, OutboxJson.Options),
            integrationEvent.OccurredAt);
    }

    internal void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    internal void MarkFailed(string error)
    {
        Attempts++;
        LastError = error;
    }
}
