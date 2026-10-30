using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.Mappers;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Contracts.V1;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

/// <summary>Registra se o pagamento foi aprovado ou recusado, e avisa a saga</summary>
/// <remarks>
/// Hoje é chamado pela rota de simulação; na etapa 6, pelo webhook do Mercado
/// Pago, que reentrega a mesma notificação mais de uma vez. Por isso é
/// idempotente: o mesmo resultado repetido não grava nada e publica de novo.
/// </remarks>
public class RegistrarResultadoDoPagamentoUseCase
{
    private readonly IPagamentoRepository _pagamentos;
    private readonly IPublicadorDeEventos _publicador;

    public RegistrarResultadoDoPagamentoUseCase(IPagamentoRepository pagamentos, IPublicadorDeEventos publicador)
    {
        _pagamentos = pagamentos;
        _publicador = publicador;
    }

    public async Task<PagamentoResponse> ExecutarAsync(Guid orcamentoId, Guid pagamentoId, ResultadoDoPagamentoRequest request)
    {
        var pagamento = await _pagamentos.ObterPorIdAsync(orcamentoId, pagamentoId)
            ?? throw new PagamentoNaoEncontradoException(pagamentoId);

        var destino = request.Aprovado ? StatusPagamento.Confirmado : StatusPagamento.Recusado;
        if (pagamento.Status != destino)
        {
            if (pagamento.Status == StatusPagamento.Criado)
                pagamento.AguardarPagamento(request.ReferenciaExterna ?? $"simulado-{pagamento.Id:N}");

            if (request.Aprovado)
                pagamento.Confirmar();
            else
                pagamento.Recusar();

            await _pagamentos.AtualizarAsync(pagamento);
        }

        if (request.Aprovado)
            await _publicador.PublicarAsync(new PagamentoConfirmado(pagamento.OrdemDeServicoId, pagamento.OrcamentoId, pagamento.Id));
        else
            await _publicador.PublicarAsync(new PagamentoRecusado(
                pagamento.OrdemDeServicoId, pagamento.OrcamentoId, pagamento.Id, request.Motivo ?? "Pagamento recusado"));

        return pagamento.ParaResponse();
    }
}
