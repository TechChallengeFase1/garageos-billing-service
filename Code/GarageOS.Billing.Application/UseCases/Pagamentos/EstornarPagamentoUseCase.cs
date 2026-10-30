using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Devolve ao cliente um pagamento confirmado (compensação da saga)</summary>
/// <remarks>
/// Idempotente: estornar um pagamento já estornado não grava nada. A devolução
/// de verdade no Mercado Pago entra na etapa 6.
/// </remarks>
public class EstornarPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentos;

    public EstornarPagamentoUseCase(IPagamentoRepository pagamentos)
    {
        _pagamentos = pagamentos;
    }

    /// <returns>O pagamento, ou null se não existe</returns>
    public async Task<PagamentoResponse?> ExecutarAsync(Guid orcamentoId, Guid pagamentoId)
    {
        var pagamento = await _pagamentos.ObterPorIdAsync(orcamentoId, pagamentoId);
        if (pagamento is null)
            return null;

        if (pagamento.Status == StatusPagamento.Confirmado)
        {
            pagamento.Estornar();
            await _pagamentos.AtualizarAsync(pagamento);
        }
        else if (pagamento.Status != StatusPagamento.Estornado)
        {
            throw new TransicaoDeStatusInvalidaException(
                $"Só um pagamento confirmado pode ser estornado. Status atual: {pagamento.Status}.");
        }

        return pagamento.ParaResponse();
    }
}
