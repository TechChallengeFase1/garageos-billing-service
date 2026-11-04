using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Fecha as cobranças não pagas de um orçamento que foi cancelado</summary>
/// <remarks>
/// Sem isto, o cliente ainda conseguiria pagar pelo link de uma OS cancelada.
/// Repetir não faz nada: quem já expirou não é mais tocado.
/// </remarks>
public class InvalidarCobrancasUseCase
{
    private readonly IPagamentoRepository _pagamentos;
    private readonly IGatewayDePagamento _gateway;

    public InvalidarCobrancasUseCase(IPagamentoRepository pagamentos, IGatewayDePagamento gateway)
    {
        _pagamentos = pagamentos;
        _gateway = gateway;
    }

    public async Task ExecutarAsync(Guid orcamentoId)
    {
        var pagamentos = await _pagamentos.ListarPorOrcamentoAsync(orcamentoId);

        foreach (var pagamento in pagamentos.Where(p => p.Status is StatusPagamento.Criado or StatusPagamento.Pendente))
        {
            if (!string.IsNullOrWhiteSpace(pagamento.PreferenciaId))
                await _gateway.ExpirarCobrancaAsync(pagamento.PreferenciaId);

            pagamento.Expirar();
            await _pagamentos.AtualizarAsync(pagamento);
        }
    }
}
