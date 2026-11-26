using System.Globalization;
using System.Text.Json.Nodes;
using Erp.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Erp.Api.OpenApi;

/// <summary>
/// Publishes every module's permission codes (the catalog <c>GET /api/permissions</c> returns) as the string enum schema
/// <c>PermissionCode</c>, with <c>x-enumNames</c> such as <c>CoreArticlesRead</c> for <c>core.articles.read</c>, so the
/// generated client carries them and a renamed or removed permission breaks the client's type check instead of
/// silently drifting (docs/open-decisions.md, Resolved).
/// </summary>
internal sealed class PermissionCodesTransformer : IOpenApiDocumentTransformer
{
    public const string SchemaName = "PermissionCode";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var codes = context.ApplicationServices.GetRequiredService<PermissionCatalog>().All;
        var names = new JsonArray();
        foreach (var code in codes)
        {
            names.Add(NameOf(code));
        }

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[SchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "A permission code a role can grant; GET /api/permissions lists the same set.",
            Enum = codes.Select(code => (JsonNode)JsonValue.Create(code)!).ToList(),
            Extensions = new Dictionary<string, IOpenApiExtension> { ["x-enumNames"] = new JsonNodeExtension(names) },
        };
        return Task.CompletedTask;
    }

    /// <summary>"core.business-partners.read" → "CoreBusinessPartnersRead".</summary>
    internal static string NameOf(string code) =>
        string.Concat(code.Split('.', '-').Where(part => part.Length > 0)
            .Select(part => char.ToUpper(part[0], CultureInfo.InvariantCulture) + part[1..]));
}
