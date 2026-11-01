using Erp.Modules.Identity.Contracts.Auth;

namespace Erp.Modules.Identity.Contracts.Registration;

/// <summary>
/// Step 1 of company registration: the registration key creates the administrator's account
/// and signs them in. Step 2 creates the company (POST /api/companies) with that session.
/// </summary>
public sealed record RegisterAccountRequest(
    string RegistrationKey,
    string Email,
    string DisplayName,
    string Password,
    RefreshTokenDelivery TokenDelivery = RefreshTokenDelivery.Cookie);
