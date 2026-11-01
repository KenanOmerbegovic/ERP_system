namespace Erp.Messaging;

/// <summary>Event types that have handlers, by the name stored in the outbox.</summary>
public sealed class IntegrationEventTypes(IEnumerable<IntegrationEventRegistration> registrations)
{
    private readonly Dictionary<string, Type> _types = registrations
        .Select(registration => registration.EventType)
        .Distinct()
        .ToDictionary(NameOf, StringComparer.Ordinal);

    public static string NameOf(Type eventType) =>
        eventType.FullName ?? throw new ArgumentException("Integration events must be named types.", nameof(eventType));

    /// <summary>Null when no module handles the event, which is not an error.</summary>
    public Type? Find(string name) => _types.GetValueOrDefault(name);
}

/// <summary>Added once per subscription by <see cref="MessagingServiceCollectionExtensions.AddIntegrationEventHandler{TEvent, THandler}"/>.</summary>
public sealed record IntegrationEventRegistration(Type EventType);
