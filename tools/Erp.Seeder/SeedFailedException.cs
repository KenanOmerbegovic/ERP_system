namespace Erp.Seeder;

/// <summary>Stops a run with a message for the developer, e.g. a rejected command or a database that is already seeded.</summary>
internal sealed class SeedFailedException(string message) : Exception(message);
