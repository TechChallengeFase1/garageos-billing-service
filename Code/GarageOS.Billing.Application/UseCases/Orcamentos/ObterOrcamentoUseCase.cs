using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Orcamentos;

public class ObterOrcamentoUseCase
{
    private readonly IOrcamentoRepository _repository;

    public ObterOrcamentoUseCase(IOrcamentoRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrcamentoResponse> ExecutarAsync(Guid id)
    {
        var orcamento = await _repository.ObterPorIdAsync(id)
            ?? throw new OrcamentoNaoEncontradoException(id);

        return orcamento.ParaResponse();
    }

    /// <returns>O orçamento da OS, ou null se ela ainda não tem</returns>
    public async Task<OrcamentoResponse?> ExecutarPorOrdemDeServicoAsync(Guid ordemDeServicoId)
    {
        var orcamento = await _repository.ObterPorOrdemDeServicoIdAsync(ordemDeServicoId);

        return orcamento?.ParaResponse();
    }
}
