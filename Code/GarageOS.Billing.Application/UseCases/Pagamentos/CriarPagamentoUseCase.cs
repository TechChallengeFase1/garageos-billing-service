using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Abre a cobrança de um orçamento aprovado no provedor de pagamento</summary>
/// <remarks>
/// Grava o pagamento antes de chamar o provedor. Se a chamada falhar, a repetição
/// encontra o pagamento em Criado e tenta de novo. O Mercado Pago não deduplica
/// preferências, então uma resposta perdida deixa um link que ninguém recebeu: ele
/// expira sozinho e, se fosse pago, cairia neste mesmo pagamento pela referência.
///
/// Havendo cobrança já aberta ou paga, devolve essa. Uma nova só é aberta se as
/// anteriores foram recusadas ou expiraram.
/// </remarks>
public class CriarPagamentoUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;
    private readonly IGatewayDePagamento _gateway;

    public CriarPagamentoUseCase(
        IOrcamentoRepository orcamentoRepository,
        IPagamentoRepository pagamentoRepository,
        IGatewayDePagamento gateway)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
        _gateway = gateway;
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
        var pagamento = pagamentos.FirstOrDefault(p => p.EmAberto);
        var criado = pagamento is null;

        if (pagamento is null)
        {
            pagamento = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, orcamento.Total);
            await _pagamentoRepository.AdicionarAsync(pagamento);
        }

        if (pagamento.Status == StatusPagamento.Criado)
        {
            var cobranca = await _gateway.CriarCobrancaAsync(new NovaCobranca(
                ReferenciaDaCobranca.Montar(pagamento),
                $"Ordem de serviço {orcamento.NumeroOS}",
                pagamento.Valor));

            pagamento.AguardarPagamento(cobranca.PreferenciaId, cobranca.LinkPagamento);
            await _pagamentoRepository.AtualizarAsync(pagamento);
        }

        return (pagamento.ParaResponse(), criado);
    }
}
