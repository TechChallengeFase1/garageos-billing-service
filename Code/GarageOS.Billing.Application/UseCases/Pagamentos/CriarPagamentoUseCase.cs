using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Abre a cobrança de um orçamento aprovado</summary>
/// <remarks>
/// Idempotente: se o orçamento já tem uma cobrança em aberto (criada, pendente
/// ou confirmada), devolve essa em vez de cobrar duas vezes. Uma nova só é
/// aberta se as anteriores foram recusadas ou expiraram.
///
/// A chamada ao Mercado Pago entra na etapa 6.
/// </remarks>
public class CriarPagamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;

    public CriarPagamentoUseCase(IOrcamentoRepository orcamentoRepository, IPagamentoRepository pagamentoRepository)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
    }

    /// <returns>O pagamento e se ele foi criado agora (false = já havia um em aberto)</returns>
    public async Task<(PagamentoResponse Pagamento, bool Criado)> ExecutarAsync(Guid orcamentoId)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(orcamentoId)
            ?? throw new OrcamentoNaoEncontradoException(orcamentoId);

        if (orcamento.Status != StatusOrcamento.Aprovado)
            throw new TransicaoDeStatusInvalidaException(
                $"Só um orçamento aprovado pode ser cobrado. Status atual: {orcamento.Status}.");

        var pagamentos = await _pagamentoRepository.ListarPorOrcamentoAsync(orcamentoId);
        var emAberto = pagamentos.FirstOrDefault(p => p.EmAberto);
        if (emAberto is not null)
            return (emAberto.ParaResponse(), false);

        var pagamento = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, orcamento.Total);
        await _pagamentoRepository.AdicionarAsync(pagamento);

        return (pagamento.ParaResponse(), true);
    }
}
