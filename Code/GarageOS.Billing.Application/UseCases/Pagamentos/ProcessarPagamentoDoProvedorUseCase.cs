using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;

namespace GarageOS.Billing.Application.UseCases.Pagamentos;

public enum ResultadoDoProcessamento
{
    /// <summary>Nada a fazer: pagamento desconhecido, em andamento ou aviso atrasado</summary>
    Ignorado,
    Aplicado,

    /// <summary>Pago numa cobrança que já estava encerrada: o valor foi devolvido</summary>
    Devolvido
}

/// <summary>Aplica ao pagamento local o que o provedor informa</summary>
/// <remarks>
/// A notificação do webhook só diz "algo mudou no pagamento X". O resultado vem
/// sempre da consulta à API do provedor: o corpo da notificação não é confiável,
/// e ela pode chegar repetida ou fora de ordem.
/// </remarks>
public class ProcessarPagamentoDoProvedorUseCase
{
    private readonly IGatewayDePagamento _gateway;
    private readonly IPagamentoRepository _pagamentos;
    private readonly RegistrarResultadoDoPagamentoUseCase _registrar;

    public ProcessarPagamentoDoProvedorUseCase(
        IGatewayDePagamento gateway,
        IPagamentoRepository pagamentos,
        RegistrarResultadoDoPagamentoUseCase registrar)
    {
        _gateway = gateway;
        _pagamentos = pagamentos;
        _registrar = registrar;
    }

    /// <summary>Caminho do webhook: chegou o aviso de um pagamento</summary>
    public async Task<ResultadoDoProcessamento> ProcessarNotificacaoAsync(string pagamentoExternoId)
    {
        var noProvedor = await _gateway.ConsultarPagamentoAsync(pagamentoExternoId);

        return noProvedor is null ? ResultadoDoProcessamento.Ignorado : await AplicarAsync(noProvedor);
    }

    /// <summary>Caminho da verificação manual: procura no provedor, sem depender do webhook</summary>
    public async Task<ResultadoDoProcessamento> VerificarAsync(Guid orcamentoId, Guid pagamentoId)
    {
        var pagamento = await _pagamentos.ObterPorIdAsync(orcamentoId, pagamentoId)
            ?? throw new PagamentoNaoEncontradoException(pagamentoId);

        var encontrados = await _gateway.BuscarPagamentosAsync(ReferenciaDaCobranca.Montar(pagamento));

        // Um cartão recusado seguido de outro aprovado na mesma cobrança: vale o aprovado.
        var maisRelevante = encontrados.FirstOrDefault(p => p.Situacao == SituacaoNoProvedor.Aprovado)
            ?? encontrados.LastOrDefault();

        return maisRelevante is null ? ResultadoDoProcessamento.Ignorado : await AplicarAsync(maisRelevante);
    }

    private async Task<ResultadoDoProcessamento> AplicarAsync(PagamentoNoProvedor noProvedor)
    {
        if (!ReferenciaDaCobranca.TentarLer(noProvedor.Referencia, out var orcamentoId, out var pagamentoId))
            return ResultadoDoProcessamento.Ignorado;

        var pagamento = await _pagamentos.ObterPorIdAsync(orcamentoId, pagamentoId);
        if (pagamento is null)
            return ResultadoDoProcessamento.Ignorado;

        switch (noProvedor.Situacao)
        {
            case SituacaoNoProvedor.Aprovado when pagamento.Status is StatusPagamento.Recusado or StatusPagamento.Expirado:
                // O cliente pagou depois de a cobrança ser encerrada. O dinheiro entrou,
                // mas a OS já foi compensada: ele volta. A chave de idempotência protege
                // contra a mesma notificação repetida.
                await _gateway.EstornarAsync(noProvedor.Id, $"tardio-{noProvedor.Id}");
                return ResultadoDoProcessamento.Devolvido;

            case SituacaoNoProvedor.Aprovado when pagamento.Status != StatusPagamento.Estornado:
                await _registrar.ExecutarAsync(orcamentoId, pagamentoId, new ResultadoDoPagamentoRequest
                {
                    Aprovado = true,
                    ReferenciaExterna = noProvedor.Id
                });
                return ResultadoDoProcessamento.Aplicado;

            case SituacaoNoProvedor.Recusado when pagamento.Status is StatusPagamento.Criado or StatusPagamento.Pendente or StatusPagamento.Recusado:
                await _registrar.ExecutarAsync(orcamentoId, pagamentoId, new ResultadoDoPagamentoRequest
                {
                    Aprovado = false,
                    Motivo = noProvedor.Detalhe,
                    ReferenciaExterna = noProvedor.Id
                });
                return ResultadoDoProcessamento.Aplicado;

            case SituacaoNoProvedor.Estornado when pagamento.Status == StatusPagamento.Confirmado:
                // Devolvido direto no painel do provedor ou contestado pelo cliente.
                pagamento.Estornar();
                await _pagamentos.AtualizarAsync(pagamento);
                return ResultadoDoProcessamento.Aplicado;

            default:
                return ResultadoDoProcessamento.Ignorado;
        }
    }
}
