namespace Erp.SharedKernel.Time;

/// <summary>Source of the current time. Never read DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow directly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>The business date: today's date in the business time zone (country localization), e.g. for document dates.</summary>
    DateOnly Today { get; }
}
