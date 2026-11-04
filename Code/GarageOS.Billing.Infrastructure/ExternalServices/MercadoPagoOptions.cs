namespace GarageOS.Billing.Infrastructure.ExternalServices;

/// <summary>Configuração do Mercado Pago (seção "MercadoPago")</summary>
/// <remarks>
/// AccessToken e WebhookSecret são segredos: vêm do Secrets Manager
/// (garageos-producao-billing/mercadopago), nunca do appsettings.
/// </remarks>
public class MercadoPagoOptions
{
    public const string Secao = "MercadoPago";

    public string BaseUrl { get; set; } = "https://api.mercadopago.com/";
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Assinatura secreta do webhook, gerada no painel do Mercado Pago</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>URL pública do webhook. Vazia em ambiente local: o Mercado Pago não alcança localhost.</summary>
    /// <remarks>
    /// Terminar com ?source_news=webhooks. Sem isso o Mercado Pago manda também o
    /// aviso antigo (IPN), que não tem assinatura e seria recusado com 401.
    /// </remarks>
    public string? NotificationUrl { get; set; }

    /// <summary>Validade da cobrança. Acompanha o prazo de pagamento da saga.</summary>
    public int ExpiracaoMinutos { get; set; } = 10;
}
