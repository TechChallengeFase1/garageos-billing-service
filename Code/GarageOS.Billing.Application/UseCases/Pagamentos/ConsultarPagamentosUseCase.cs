using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

public class ConsultarPagamentosUseCase
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IPagamentoRepository _pagamentoRepository;

    public ConsultarPagamentosUseCase(IOrcamentoRepository orcamentoRepository, IPagamentoRepository pagamentoRepository)
    {
        _orcamentoRepository = orcamentoRepository;
        _pagamentoRepository = pagamentoRepository;
    }

    public async Task<IEnumerable<PagamentoResponse>> ListarAsync(Guid orcamentoId)
    {
        _ = await _orcamentoRepository.ObterPorIdAsync(orcamentoId)
            ?? throw new OrcamentoNaoEncontradoException(orcamentoId);

        var pagamentos = await _pagamentoRepository.ListarPorOrcamentoAsync(orcamentoId);

        return pagamentos.OrderBy(p => p.CriadoEm).Select(p => p.ParaResponse());
    }

    public async Task<PagamentoResponse> ObterAsync(Guid orcamentoId, Guid pagamentoId)
    {
        var pagamento = await _pagamentoRepository.ObterPorIdAsync(orcamentoId, pagamentoId)
            ?? throw new PagamentoNaoEncontradoException(pagamentoId);

        return pagamento.ParaResponse();
    }
}
