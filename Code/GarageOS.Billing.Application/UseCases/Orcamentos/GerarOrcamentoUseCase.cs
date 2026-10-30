using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.Domain.ValueObjects;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

/// <summary>Gera o orçamento de uma OS a partir dos itens com preço</summary>
/// <remarks>
/// Idempotente por OS: um orçamento por ordem de serviço. Pedir de novo
/// devolve o que já existe, inclusive quando duas requisições chegam ao mesmo
/// tempo (a persistência só deixa uma gravar).
/// </remarks>
public class GerarOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;
    private readonly IMetricasDeNegocio _metricas;

    public GerarOrcamentoUseCase(IOrcamentoRepository repository, IMetricasDeNegocio metricas)
    {
        _repository = repository;
        _metricas = metricas;
    }

    /// <returns>O orçamento e se ele foi criado agora (false = já existia)</returns>
    public async Task<(OrcamentoResponse Orcamento, bool Criado)> ExecutarAsync(GerarOrcamentoRequest request)
    {
        var existente = await _repository.ObterPorOrdemDeServicoIdAsync(request.OrdemDeServicoId);
        if (existente is not null)
            return (existente.ParaResponse(), false);

        var orcamento = new Orcamento(
            request.OrdemDeServicoId,
            request.ClienteId,
            request.NumeroOS,
            request.Itens.Select(i => new ItemOrcamento(i.Tipo, i.ReferenciaId, i.Descricao, i.Quantidade, i.ValorUnitario)));

        try
        {
            await _repository.AdicionarAsync(orcamento);
        }
        catch (OrcamentoJaExisteException)
        {
            var gravado = await _repository.ObterPorOrdemDeServicoIdAsync(request.OrdemDeServicoId)
                ?? throw new ConflitoDeConcorrenciaException("O orçamento da OS foi gravado e removido ao mesmo tempo.");
            return (gravado.ParaResponse(), false);
        }

        _metricas.OrcamentoGerado(orcamento.NumeroOS, orcamento.Total);

        return (orcamento.ParaResponse(), true);
    }
}
