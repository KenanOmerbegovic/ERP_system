using System.ComponentModel.DataAnnotations;

namespace Erp.Messaging.Outbox;

/// <summary>Configuration section <c>Messaging:Outbox</c>.</summary>
public sealed class OutboxOptions : IValidatableObject
{
    public const string SectionName = "Messaging:Outbox";

    /// <summary>Off only where nothing may be delivered, e.g. build-time OpenAPI generation.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How soon each module's outbox is checked again after a check that found messages.</summary>
    [Range(typeof(TimeSpan), "00:00:00.050", "00:10:00")]
    public TimeSpan PollInterval { get; set; }

    /// <summary>
    /// Longest wait between checks while the outbox stays empty (or unreachable): each empty check doubles the
    /// wait, from <see cref="PollInterval"/> up to this. A new event can wait this long before it is delivered.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.050", "01:00:00")]
    public TimeSpan MaxIdlePollInterval { get; set; }

    [Range(1, 1000)]
    public int BatchSize { get; set; }

    /// <summary>After this many failed deliveries a message stays in the outbox for inspection.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; }

    /// <summary>The wait before the next check: back to <see cref="PollInterval"/> once there is work, doubling while idle.</summary>
    public TimeSpan NextPollDelay(TimeSpan previousDelay, bool foundMessages)
    {
        if (foundMessages)
        {
            return PollInterval;
        }

        var doubled = previousDelay * 2;
        return doubled < PollInterval ? PollInterval : doubled > MaxIdlePollInterval ? MaxIdlePollInterval : doubled;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxIdlePollInterval < PollInterval)
        {
            yield return new ValidationResult(
                $"{nameof(MaxIdlePollInterval)} must not be shorter than {nameof(PollInterval)}.",
                [nameof(MaxIdlePollInterval)]);
        }
    }
}
