using Erp.Modules.Core.Application.Articles;
using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Contracts.Users;
using Erp.Seeder.Data;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Erp.Seeder;

internal sealed record SeededCompany(CompanyId CompanyId, Guid AdministratorId, IReadOnlyDictionary<string, Guid> RoleIds);

/// <summary>
/// Creates the mock data only through Application commands, in the order a real company would:
/// registration key, administrator account, company, then roles, users and master data.
/// Each command runs in its own scope, like an HTTP request.
/// </summary>
internal sealed class MockDataSeeder(IServiceProvider services)
{
    private static readonly TimeSpan MembershipTimeout = TimeSpan.FromSeconds(30);

    public async Task<SeededCompany> SeedAsync(SeedData data, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        var key = await SendAsync<IssueRegistrationKeyHandler, IssuedRegistrationKey>(
            (handler, ct) => handler.HandleAsync(new IssueRegistrationKey(), ct), "Issue registration key", cancellationToken);
        var tokens = await SendAsync<RegisterAccountHandler, AuthTokens>(
            (handler, ct) => handler.HandleAsync(
                new RegisterAccount(key.Key, data.Administrator.Email, data.Administrator.DisplayName, password), ct),
            $"Register {data.Administrator.Email}",
            cancellationToken);

        // The same id the API reads from the access token's "sub" claim.
        var administratorId = Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(tokens.AccessToken).Subject);

        var company = data.Company;
        var companyId = await SendAsync<CreateCompanyHandler, CompanyId>(
            (handler, ct) => handler.HandleAsync(
                new CreateCompany(administratorId, company.Name, company.TaxId, company.VatNumber, company.Address, company.PostalCode, company.City), ct),
            $"Create company {company.Name}",
            cancellationToken);
        await WaitForMembershipAsync(administratorId, companyId, cancellationToken);

        var roleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var role in data.Roles)
        {
            roleIds[role.Name] = await SendAsync<CreateRoleHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateRole(companyId, role.Name, role.Permissions), ct), $"Create role {role.Name}", cancellationToken);
            await SendAsync<CreateUserHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateUser(companyId, role.User.Email, role.User.DisplayName, password, [roleIds[role.Name]]), ct),
                $"Create user {role.User.Email}",
                cancellationToken);
        }

        var unitIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var unit in data.Units)
        {
            unitIds[unit.Code] = await SendAsync<CreateUnitOfMeasureHandler, Guid>(
                (handler, ct) => handler.HandleAsync(new CreateUnitOfMeasure(companyId, unit.Code, unit.Name), ct), $"Create unit {unit.Code}", cancellationToken);
        }

        foreach (var partner in data.Partners)
        {
            await SendAsync<CreateBusinessPartnerHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateBusinessPartner(
                        companyId,
                        partner.Code,
                        partner.Name,
                        partner.TaxId,
                        partner.VatNumber,
                        partner.Address,
                        partner.PostalCode,
                        partner.City,
                        CountryCode: null,
                        partner.IsCustomer,
                        partner.IsSupplier,
                        partner.IsActive),
                    ct),
                $"Create business partner {partner.Code}",
                cancellationToken);
        }

        foreach (var article in data.Articles)
        {
            await SendAsync<CreateArticleHandler, Guid>(
                (handler, ct) => handler.HandleAsync(
                    new CreateArticle(companyId, article.Code, article.Name, article.Type, unitIds[article.UnitCode], article.IsActive), ct),
                $"Create article {article.Code}",
                cancellationToken);
        }

        return new SeededCompany(companyId, administratorId, roleIds);
    }

    /// <summary>The administrator's membership arrives through Core's outbox (CompanyCreated), moments after the company.</summary>
    private async Task WaitForMembershipAsync(Guid userId, CompanyId companyId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + MembershipTimeout;
        while (true)
        {
            var me = await SendAsync<GetCurrentUserHandler, CurrentUserResponse>(
                (handler, ct) => handler.HandleAsync(new GetCurrentUser(userId), ct), "Read the administrator", cancellationToken);
            if (me.Companies.Any(membership => membership.CompanyId == companyId.Value))
            {
                return;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new SeedFailedException("The administrator did not become a member of the company; is the outbox processor running?");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private async Task<T> SendAsync<THandler, T>(
        Func<THandler, CancellationToken, Task<Result<T>>> send,
        string step,
        CancellationToken cancellationToken)
        where THandler : notnull
    {
        await using var scope = services.CreateAsyncScope();
        var result = await send(scope.ServiceProvider.GetRequiredService<THandler>(), cancellationToken);
        return result.IsSuccess ? result.Value : throw new SeedFailedException($"{step} failed: {Describe(result.Error)}");
    }

    internal static string Describe(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Errors.Select(field => $"{field.Key}: {string.Join(", ", field.Value)}"))
        : $"{error.Code} ({error.Message})";
}
