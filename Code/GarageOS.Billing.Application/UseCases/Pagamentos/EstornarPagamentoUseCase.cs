using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Devolve ao cliente um pagamento confirmado (compensação da saga)</summary>
/// <remarks>
/// Primeiro devolve no provedor, depois grava. Se a gravação falhar, a repetição
/// chama o provedor de novo com a mesma chave de idempotência, sem estorno em dobro.
/// Estornar um pagamento já estornado não faz nada.
/// </remarks>
public class EstornarPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentos;
    private readonly IGatewayDePagamento _gateway;

    public EstornarPagamentoUseCase(IPagamentoRepository pagamentos, IGatewayDePagamento gateway)
    {
        _pagamentos = pagamentos;
        _gateway = gateway;
    }

    /// <returns>O pagamento, ou null se não existe</returns>
    public async Task<PagamentoResponse?> ExecutarAsync(Guid orcamentoId, Guid pagamentoId)
    {
        var pagamento = await _pagamentos.ObterPorIdAsync(orcamentoId, pagamentoId);
        if (pagamento is null)
            return null;

        if (pagamento.Status == StatusPagamento.Confirmado)
        {
            if (!string.IsNullOrWhiteSpace(pagamento.ReferenciaExterna))
                await _gateway.EstornarAsync(pagamento.ReferenciaExterna, $"estorno-{pagamento.Id}");

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
