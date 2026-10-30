using GarageOS.Contracts.V1;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Domain.Exceptions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GarageOS.Billing.Infrastructure.Mensageria.Consumidores;

/// <summary>Compensação da saga: devolver o pagamento confirmado</summary>
/// <remarks>Idempotente: pagamento já estornado é aceito sem erro.</remarks>
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    private readonly EstornarPagamentoUseCase _estornar;
    private readonly ILogger<EstornarPagamentoConsumer> _logger;

    public EstornarPagamentoConsumer(EstornarPagamentoUseCase estornar, ILogger<EstornarPagamentoConsumer> logger)
    {
        _estornar = estornar;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var m = context.Message;
        try
        {
            var pagamento = await _estornar.ExecutarAsync(m.OrcamentoId, m.PagamentoId);
            if (pagamento is null)
            {
                _logger.LogWarning("Pagamento {PagamentoId} nao encontrado para estorno", m.PagamentoId);
                return;
            }
        }
        catch (TransicaoDeStatusInvalidaException ex)
        {
            _logger.LogWarning(ex, "Pagamento {PagamentoId} nao confirmado; estorno ignorado", m.PagamentoId);
            return;
        }

        await context.Publish(new PagamentoEstornado(m.OrdemDeServicoId, m.PagamentoId));
    }
}
