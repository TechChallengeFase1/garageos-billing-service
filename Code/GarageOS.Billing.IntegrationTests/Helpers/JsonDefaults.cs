using System.Text.Json;
using System.Text.Json.Serialization;

namespace GarageOS.Billing.IntegrationTests.Helpers;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
