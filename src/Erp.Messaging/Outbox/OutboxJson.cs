using System.Text.Json;

namespace Erp.Messaging.Outbox;

internal static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
