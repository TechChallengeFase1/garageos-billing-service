using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

/// <summary>Registra a aprovação ou a recusa do cliente</summary>
/// <remarks>
/// Só aqui o orçamento muda. Baixa de estoque e mudança de status da OS, que o
/// monólito fazia no mesmo SaveChanges, agora são passos da saga em outros
/// serviços (etapa 5).
/// </remarks>
public class ResponderOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;
    private readonly IMetricasDeNegocio _metricas;

    public ResponderOrcamentoUseCase(IOrcamentoRepository repository, IMetricasDeNegocio metricas)
    {
        _repository = repository;
        _metricas = metricas;
    }

    public async Task<OrcamentoResponse> ExecutarAsync(Guid id, ResponderOrcamentoRequest request)
    {
        var orcamento = await _repository.ObterPorIdAsync(id)
            ?? throw new OrcamentoNaoEncontradoException(id);

        var anterior = orcamento.Status;
        if (request.Aprovado)
            orcamento.Aprovar();
        else
            orcamento.Recusar();

        await _repository.AtualizarAsync(orcamento);
        _metricas.OrcamentoStatusAlterado(orcamento.NumeroOS, anterior.ToString(), orcamento.Status.ToString());

        return orcamento.ParaResponse();
    }
}
