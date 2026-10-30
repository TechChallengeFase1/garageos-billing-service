using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Contracts.V1;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

/// <summary>Registra a aprovação ou a recusa do cliente e avisa a saga</summary>
/// <remarks>
/// Só aqui o orçamento muda. Reserva de peças, cobrança e status da OS, que o
/// monólito fazia no mesmo SaveChanges, são passos da saga em outros serviços.
///
/// Idempotente: repetir a mesma resposta não grava nada e publica o evento de
/// novo. É o que cobre a publicação que falhou depois de gravar. Mudar de
/// ideia (recusar o que já foi aprovado) continua sendo 409.
/// </remarks>
public class ResponderOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;
    private readonly IMetricasDeNegocio _metricas;
    private readonly IPublicadorDeEventos _publicador;

    public ResponderOrcamentoUseCase(IOrcamentoRepository repository, IMetricasDeNegocio metricas, IPublicadorDeEventos publicador)
    {
        _repository = repository;
        _metricas = metricas;
        _publicador = publicador;
    }

    public async Task<OrcamentoResponse> ExecutarAsync(Guid id, ResponderOrcamentoRequest request)
    {
        var orcamento = await _repository.ObterPorIdAsync(id)
            ?? throw new OrcamentoNaoEncontradoException(id);

        var pedido = request.Aprovado ? StatusOrcamento.Aprovado : StatusOrcamento.Recusado;
        if (orcamento.Status != pedido)
        {
            var anterior = orcamento.Status;
            if (request.Aprovado)
                orcamento.Aprovar();
            else
                orcamento.Recusar();

            await _repository.AtualizarAsync(orcamento);
            _metricas.OrcamentoStatusAlterado(orcamento.NumeroOS, anterior.ToString(), orcamento.Status.ToString());
        }

        if (request.Aprovado)
            await _publicador.PublicarAsync(new OrcamentoAprovado(orcamento.OrdemDeServicoId, orcamento.Id));
        else
            await _publicador.PublicarAsync(new OrcamentoRecusado(orcamento.OrdemDeServicoId, orcamento.Id));

        return orcamento.ParaResponse();
    }
}
