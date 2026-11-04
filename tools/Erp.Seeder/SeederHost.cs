using System.Security.Cryptography;
using Erp.Localization.BiH.Core;
using Erp.Modules.Core;
using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Identity;
using Erp.SharedKernel.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Erp.Seeder;

/// <summary>The modules composed as in Erp.Api, without HTTP. Core's outbox processor runs while the host is started.</summary>
internal static class SeederHost
{
    /// <param name="settings">Added last, e.g. a test database's connection string.</param>
    public static IHost Build(IReadOnlyDictionary<string, string?>? settings = null)
    {
        // appsettings.json is read from the build output, wherever the seeder is started from.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });

        // Registration signs the administrator in; the seeder discards those tokens, so any fresh key will do.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Jwt:SigningKey"] = RandomNumberGenerator.GetHexString(64),
        });
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ITaxIdentifierRules, BiHTaxIdentifierRules>();
        builder.Services.AddIdentityModule(builder.Configuration, CorePermissions.All);
        builder.Services.AddCoreModule(builder.Configuration);
        return builder.Build();
    }
}
