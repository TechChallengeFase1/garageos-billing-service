namespace GarageOS.Billing.Application.Abstractions;

/// <summary>Confere se uma notificação de pagamento veio mesmo do provedor</summary>
public interface IValidadorDeWebhook
{
    bool Valido(string? assinatura, string? requestId, string? dataId);
}
