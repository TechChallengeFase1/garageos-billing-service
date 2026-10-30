using GarageOS.Contracts.V1;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Domain.Exceptions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GarageOS.Billing.Infrastructure.Mensageria.Consumidores;

/// <summary>Compensação da saga: cancelar o orçamento</summary>
/// <remarks>
/// Idempotente. Sem orçamento para a OS, ou já recusado pelo cliente, não há o
/// que cancelar: a mensagem é aceita sem erro.
/// </remarks>
public class CancelarOrcamentoConsumer : IConsumer<CancelarOrcamento>
{
    private readonly ObterOrcamentoUseCase _obter;
    private readonly CancelarOrcamentoUseCase _cancelar;
    private readonly ILogger<CancelarOrcamentoConsumer> _logger;

    public CancelarOrcamentoConsumer(ObterOrcamentoUseCase obter, CancelarOrcamentoUseCase cancelar, ILogger<CancelarOrcamentoConsumer> logger)
    {
        _obter = obter;
        _cancelar = cancelar;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CancelarOrcamento> context)
    {
        var osId = context.Message.OrdemDeServicoId;
        var orcamento = await _obter.ExecutarPorOrdemDeServicoAsync(osId);
        if (orcamento is null)
        {
            _logger.LogInformation("OS {OrdemDeServicoId} sem orcamento: nada a cancelar", osId);
            return;
        }

        try
        {
            await _cancelar.ExecutarAsync(orcamento.Id, new CancelarOrcamentoRequest { Motivo = context.Message.Motivo });
        }
        catch (TransicaoDeStatusInvalidaException ex)
        {
            _logger.LogInformation(ex, "Orcamento {OrcamentoId} ja encerrado; cancelamento ignorado", orcamento.Id);
            return;
        }

        await context.Publish(new OrcamentoCancelado(osId, orcamento.Id));
    }
}
