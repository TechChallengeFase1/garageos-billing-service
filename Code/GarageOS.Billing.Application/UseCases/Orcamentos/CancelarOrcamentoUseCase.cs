using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

/// <summary>Cancela o orçamento</summary>
/// <remarks>
/// Compensação da saga (peça em falta, pagamento recusado, prazo estourado).
/// Idempotente: cancelar um orçamento já cancelado não grava nada.
/// </remarks>
public class CancelarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;
    private readonly IMetricasDeNegocio _metricas;

    public CancelarOrcamentoUseCase(IOrcamentoRepository repository, IMetricasDeNegocio metricas)
    {
        _repository = repository;
        _metricas = metricas;
    }

    public async Task<OrcamentoResponse> ExecutarAsync(Guid id, CancelarOrcamentoRequest request)
    {
        var orcamento = await _repository.ObterPorIdAsync(id)
            ?? throw new OrcamentoNaoEncontradoException(id);

        var anterior = orcamento.Status;
        if (orcamento.Cancelar(request.Motivo))
        {
            await _repository.AtualizarAsync(orcamento);
            _metricas.OrcamentoStatusAlterado(orcamento.NumeroOS, anterior.ToString(), orcamento.Status.ToString());
        }

        return orcamento.ParaResponse();
    }
}
