namespace GarageOS.Billing.Domain.Exceptions;

/// <summary>O provedor de pagamento recusou o pedido ou não respondeu</summary>
/// <remarks>
/// Vira 503 na API. Num consumidor, sobe como exceção e o retry do MassTransit
/// tenta de novo. Se o provedor seguir fora, o prazo de pagamento da saga vence
/// e a compensação roda.
/// </remarks>
public class GatewayDePagamentoException : Exception
{
    public GatewayDePagamentoException(string mensagem, Exception? interna = null)
        : base(mensagem, interna) { }
}
