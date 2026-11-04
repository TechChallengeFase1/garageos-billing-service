using GarageOS.Billing.Domain.Entities;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Referência enviada ao provedor, que volta em cada pagamento</summary>
/// <remarks>
/// O pagamento fica na partição do orçamento, então os dois ids são necessários
/// para encontrá-lo quando uma notificação chegar.
/// </remarks>
public static class ReferenciaDaCobranca
{
    private const char Separador = ':';

    public static string Montar(Pagamento pagamento) => $"{pagamento.OrcamentoId}{Separador}{pagamento.Id}";

    public static bool TentarLer(string? referencia, out Guid orcamentoId, out Guid pagamentoId)
    {
        orcamentoId = pagamentoId = Guid.Empty;
        var partes = referencia?.Split(Separador);

        return partes is { Length: 2 }
            && Guid.TryParse(partes[0], out orcamentoId)
            && Guid.TryParse(partes[1], out pagamentoId);
    }
}
