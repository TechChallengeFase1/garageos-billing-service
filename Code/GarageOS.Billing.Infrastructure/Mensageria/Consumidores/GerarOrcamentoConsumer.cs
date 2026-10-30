using GarageOS.Contracts.V1;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Domain.Enums;
using MassTransit;

namespace GarageOS.Billing.Infrastructure.Mensageria.Consumidores;

/// <summary>Passo da saga: gerar o orçamento e enviá-lo ao cliente</summary>
/// <remarks>
/// Idempotente: o orçamento é único por OS. Repetida, a mensagem encontra o
/// orçamento já gerado (e já enviado) e só publica OrcamentoEnviado de novo.
/// </remarks>
public class GerarOrcamentoConsumer : IConsumer<GerarOrcamento>
{
    private readonly GerarOrcamentoUseCase _gerar;
    private readonly EnviarOrcamentoUseCase _enviar;

    public GerarOrcamentoConsumer(GerarOrcamentoUseCase gerar, EnviarOrcamentoUseCase enviar)
    {
        _gerar = gerar;
        _enviar = enviar;
    }

    public async Task Consume(ConsumeContext<GerarOrcamento> context)
    {
        var pedido = context.Message;

        var (orcamento, _) = await _gerar.ExecutarAsync(new GerarOrcamentoRequest
        {
            OrdemDeServicoId = pedido.OrdemDeServicoId,
            ClienteId = pedido.ClienteId,
            NumeroOS = pedido.NumeroOS,
            Itens = pedido.Itens.Select(i => new ItemOrcamentoRequest
            {
                Tipo = Enum.Parse<TipoItemOrcamento>(i.Tipo),
                ReferenciaId = i.ReferenciaId,
                Descricao = i.Descricao,
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario
            }).ToList()
        });

        if (orcamento.Status == nameof(StatusOrcamento.Pendente))
            orcamento = await _enviar.ExecutarAsync(orcamento.Id);

        await context.Publish(new OrcamentoEnviado(pedido.OrdemDeServicoId, orcamento.Id, orcamento.Total));
    }
}
