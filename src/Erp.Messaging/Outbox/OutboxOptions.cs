using System.ComponentModel.DataAnnotations;

namespace Erp.Messaging.Outbox;

/// <summary>Configuration section <c>Messaging:Outbox</c>.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Messaging:Outbox";

    /// <summary>Off only where nothing may be delivered, e.g. build-time OpenAPI generation.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often each module's outbox is checked for new messages.</summary>
    [Range(typeof(TimeSpan), "00:00:00.050", "00:10:00")]
    public TimeSpan PollInterval { get; set; }

    [Range(1, 1000)]
    public int BatchSize { get; set; }

    /// <summary>After this many failed deliveries a message stays in the outbox for inspection.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; }
}
