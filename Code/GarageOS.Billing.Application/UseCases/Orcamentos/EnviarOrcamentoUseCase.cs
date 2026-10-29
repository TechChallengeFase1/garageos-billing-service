using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

/// <summary>Envia o orçamento ao cliente para aprovação</summary>
/// <remarks>
/// O envio de verdade (e-mail, notificação) fica fora do escopo: o cliente
/// consulta o orçamento pelo acompanhamento da OS e responde por aqui.
/// </remarks>
public class EnviarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;
    private readonly IMetricasDeNegocio _metricas;

    public EnviarOrcamentoUseCase(IOrcamentoRepository repository, IMetricasDeNegocio metricas)
    {
        _repository = repository;
        _metricas = metricas;
    }

    public async Task<OrcamentoResponse> ExecutarAsync(Guid id)
    {
        var orcamento = await _repository.ObterPorIdAsync(id)
            ?? throw new OrcamentoNaoEncontradoException(id);

        var anterior = orcamento.Status;
        orcamento.Enviar();

        await _repository.AtualizarAsync(orcamento);
        _metricas.OrcamentoStatusAlterado(orcamento.NumeroOS, anterior.ToString(), orcamento.Status.ToString());

        return orcamento.ParaResponse();
    }
}
