using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Identity.Application.Users;
using Erp.Seeder.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Seeder;

/// <summary>Reads the seeded company back through Application queries and compares it with what was generated.</summary>
internal static class SeedChecks
{
    public static async Task<IReadOnlyList<string>> RunAsync(
        IServiceProvider services,
        SeedData data,
        SeededCompany seeded,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(seeded);

        var problems = new List<string>();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var partners = await provider.GetRequiredService<ListBusinessPartnersHandler>()
            .HandleAsync(new ListBusinessPartners(seeded.CompanyId, null, 1, 1), cancellationToken);
        Expect(problems, "business partners", data.Partners.Count, partners.IsSuccess ? partners.Value.TotalCount : -1);

        var articles = await provider.GetRequiredService<ListArticlesHandler>()
            .HandleAsync(new ListArticles(seeded.CompanyId, null, 1, 1), cancellationToken);
        Expect(problems, "articles", data.Articles.Count, articles.IsSuccess ? articles.Value.TotalCount : -1);

        var units = await provider.GetRequiredService<ListUnitsOfMeasureHandler>()
            .HandleAsync(new ListUnitsOfMeasure(seeded.CompanyId), cancellationToken);
        Expect(problems, "units of measure", data.Units.Count, units.IsSuccess ? units.Value.Count : -1);

        var members = await provider.GetRequiredService<ListMembersHandler>()
            .HandleAsync(new ListMembers(seeded.CompanyId), cancellationToken);
        Expect(problems, "members", data.Roles.Count + 1, members.Count);
        foreach (var role in data.Roles)
        {
            var member = members.SingleOrDefault(m => string.Equals(m.Email, role.User.Email, StringComparison.OrdinalIgnoreCase));
            if (member is null || !member.IsActive || !member.RoleIds.SequenceEqual([seeded.RoleIds[role.Name]]))
            {
                problems.Add($"{role.User.Email} should be an active member with only the role {role.Name}.");
            }
        }

        var administrator = await provider.GetRequiredService<GetCurrentUserHandler>()
            .HandleAsync(new GetCurrentUser(seeded.AdministratorId), cancellationToken);
        if (!administrator.IsSuccess || !administrator.Value.Companies.Any(c => c.CompanyId == seeded.CompanyId.Value && c.IsAdministrator))
        {
            problems.Add($"{data.Administrator.Email} should be the company's administrator.");
        }

        return problems;
    }

    private static void Expect(List<string> problems, string what, int expected, int actual)
    {
        if (expected != actual)
        {
            problems.Add($"Expected {expected} {what}, found {actual}.");
        }
    }
}
