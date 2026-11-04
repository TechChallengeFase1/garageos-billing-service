using System.Security.Cryptography;
using System.Text;
using GarageOS.Billing.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.Infrastructure.ExternalServices;

/// <summary>Confere a assinatura x-signature das notificações do Mercado Pago</summary>
/// <remarks>
/// O webhook é a única rota pública sem JWT, e esta assinatura é a defesa dela.
/// O cabeçalho tem o formato "ts=1704908010,v1=abc...", e v1 é o HMAC-SHA256,
/// com a assinatura secreta do painel, de:
///
///   id:{data.id};request-id:{x-request-id};ts:{ts};
///
/// Sem segredo configurado, nada passa. Uma notificação repetida com assinatura
/// válida é inofensiva: o resultado vem da API do Mercado Pago, e aplicá-lo de
/// novo não muda nada.
/// </remarks>
public class ValidadorDeWebhookMercadoPago : IValidadorDeWebhook
{
    private readonly MercadoPagoOptions _opcoes;

    public ValidadorDeWebhookMercadoPago(IOptions<MercadoPagoOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public bool Valido(string? assinatura, string? requestId, string? dataId)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.WebhookSecret) || string.IsNullOrWhiteSpace(assinatura))
            return false;

        string? ts = null, v1 = null;
        foreach (var parte in assinatura.Split(','))
        {
            var chaveValor = parte.Split('=', 2);
            if (chaveValor.Length != 2)
                continue;

            switch (chaveValor[0].Trim())
            {
                case "ts": ts = chaveValor[1].Trim(); break;
                case "v1": v1 = chaveValor[1].Trim(); break;
            }
        }

        if (ts is null || v1 is null)
            return false;

        // Campo ausente sai do texto assinado; id alfanumérico vai em minúsculas.
        var texto = new StringBuilder();
        if (!string.IsNullOrEmpty(dataId))
            texto.Append($"id:{dataId.ToLowerInvariant()};");
        if (!string.IsNullOrEmpty(requestId))
            texto.Append($"request-id:{requestId};");
        texto.Append($"ts:{ts};");

        var esperado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_opcoes.WebhookSecret), Encoding.UTF8.GetBytes(texto.ToString()));

        // Comparação em tempo constante, para o tempo de resposta não revelar a assinatura.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Convert.ToHexStringLower(esperado)),
            Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }
}
