using GarageOS.Contracts.V1;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using MassTransit;

namespace GarageOS.Billing.Infrastructure.Mensageria.Consumidores;

/// <summary>Passo da saga: abrir a cobrança do orçamento aprovado</summary>
/// <remarks>
/// Idempotente: havendo cobrança em aberto, é ela que volta. O resultado
/// (confirmado ou recusado) chega depois, pela rota de simulação ou, na etapa
/// 6, pelo webhook do Mercado Pago.
/// </remarks>
public class SolicitarPagamentoConsumer : IConsumer<SolicitarPagamento>
{
    private readonly CriarPagamentoUseCase _criar;

    public SolicitarPagamentoConsumer(CriarPagamentoUseCase criar)
    {
        _criar = criar;
    }

    public async Task Consume(ConsumeContext<SolicitarPagamento> context)
    {
        var (pagamento, _) = await _criar.ExecutarAsync(context.Message.OrcamentoId);

        await context.Publish(new PagamentoCriado(context.Message.OrdemDeServicoId, pagamento.OrcamentoId, pagamento.Id, pagamento.Valor));
    }
}
