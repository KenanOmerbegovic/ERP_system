namespace Erp.SharedKernel.Time;

/// <summary>Source of the current time. Never read DateTime.Now or DateTimeOffset.UtcNow directly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
