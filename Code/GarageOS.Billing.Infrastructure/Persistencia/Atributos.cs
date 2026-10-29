using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace GarageOS.Billing.Infrastructure.Persistencia;

/// <summary>Conversão entre tipos .NET e AttributeValue</summary>
/// <remarks>
/// Mapeamento explícito, sem o modelo de objetos do SDK: o domínio continua
/// sem atributo nenhum do DynamoDB, e o formato gravado fica visível aqui.
/// Números vão como texto invariável (é assim que o DynamoDB os trafega).
/// </remarks>
internal static class Atributos
{
    public static AttributeValue Texto(string valor) => new() { S = valor };
    public static AttributeValue Numero(decimal valor) => new() { N = valor.ToString(CultureInfo.InvariantCulture) };
    public static AttributeValue Numero(long valor) => new() { N = valor.ToString(CultureInfo.InvariantCulture) };
    public static AttributeValue Data(DateTime valor) => new() { S = valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };

    public static void TextoOpcional(Dictionary<string, AttributeValue> item, string nome, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
            item[nome] = Texto(valor);
    }

    public static void DataOpcional(Dictionary<string, AttributeValue> item, string nome, DateTime? valor)
    {
        if (valor.HasValue)
            item[nome] = Data(valor.Value);
    }

    public static string LerTexto(this Dictionary<string, AttributeValue> item, string nome) => item[nome].S;

    public static string? LerTextoOpcional(this Dictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor) ? valor.S : null;

    public static Guid LerGuid(this Dictionary<string, AttributeValue> item, string nome) => Guid.Parse(item[nome].S);

    public static decimal LerDecimal(this Dictionary<string, AttributeValue> item, string nome) =>
        decimal.Parse(item[nome].N, CultureInfo.InvariantCulture);

    public static long LerLong(this Dictionary<string, AttributeValue> item, string nome) =>
        long.Parse(item[nome].N, CultureInfo.InvariantCulture);

    public static DateTime LerData(this Dictionary<string, AttributeValue> item, string nome) =>
        DateTime.Parse(item[nome].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTime? LerDataOpcional(this Dictionary<string, AttributeValue> item, string nome) =>
        item.TryGetValue(nome, out var valor)
            ? DateTime.Parse(valor.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    public static TEnum LerEnum<TEnum>(this Dictionary<string, AttributeValue> item, string nome) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(item[nome].S);
}
