using Erp.Modules.Core.Domain.Articles;

namespace Erp.Seeder.Data;

/// <summary>Everything one seeding run creates, generated up front so it can be checked without a database.</summary>
internal sealed record SeedData(
    CompanySeed Company,
    UserSeed Administrator,
    IReadOnlyList<RoleSeed> Roles,
    IReadOnlyList<UnitSeed> Units,
    IReadOnlyList<PartnerSeed> Partners,
    IReadOnlyList<ArticleSeed> Articles);

internal sealed record CompanySeed(string Name, string TaxId, string VatNumber, string Address, string PostalCode, string City);

internal sealed record UserSeed(string Email, string DisplayName);

/// <summary>A role and the one user who holds it.</summary>
internal sealed record RoleSeed(string Name, IReadOnlyList<string> Permissions, UserSeed User);

internal sealed record UnitSeed(string Code, string Name);

internal sealed record PartnerSeed(
    string Code,
    string Name,
    string TaxId,
    string? VatNumber,
    string Address,
    string PostalCode,
    string City,
    bool IsCustomer,
    bool IsSupplier,
    bool IsActive);

internal sealed record ArticleSeed(string Code, string Name, ArticleType Type, string UnitCode, bool IsActive);
