using FluentAssertions;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Contracts.V1;
using Moq;

namespace GarageOS.Billing.UnitTests.Application;

public class ProcessarPagamentoDoProvedorTests
{
    private readonly Mock<IGatewayDePagamento> _gateway = new();
    private readonly Mock<IPagamentoRepository> _pagamentos = new();
    private readonly Mock<IPublicadorDeEventos> _publicador = new();
    private readonly Pagamento _pagamento = new(Guid.NewGuid(), Guid.NewGuid(), 300m);

    public ProcessarPagamentoDoProvedorTests()
    {
        _pagamento.AguardarPagamento("pref-1", "https://mp.test/pagar");
        _pagamentos.Setup(r => r.ObterPorIdAsync(_pagamento.OrcamentoId, _pagamento.Id)).ReturnsAsync(_pagamento);
    }

    private ProcessarPagamentoDoProvedorUseCase Processar() => new(
        _gateway.Object, _pagamentos.Object, new RegistrarResultadoDoPagamentoUseCase(_pagamentos.Object, _publicador.Object));

    private void NoProvedor(SituacaoNoProvedor situacao, string? detalhe = null, string? referencia = "padrao") =>
        _gateway.Setup(g => g.ConsultarPagamentoAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagamentoNoProvedor("123", situacao, detalhe,
                referencia == "padrao" ? ReferenciaDaCobranca.Montar(_pagamento) : referencia));

    [Fact]
    public async Task Aprovado_DeveConfirmarGuardarOIdEAvisarASaga()
    {
        NoProvedor(SituacaoNoProvedor.Aprovado);

        var resultado = await Processar().ProcessarNotificacaoAsync("123");

        resultado.Should().Be(ResultadoDoProcessamento.Aplicado);
        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        _pagamento.ReferenciaExterna.Should().Be("123");
        _publicador.Verify(p => p.PublicarAsync(It.Is<PagamentoConfirmado>(e => e.PagamentoId == _pagamento.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificacaoRepetida_NaoDeveGravarDeNovo()
    {
        NoProvedor(SituacaoNoProvedor.Aprovado);

        await Processar().ProcessarNotificacaoAsync("123");
        await Processar().ProcessarNotificacaoAsync("123");

        _pagamentos.Verify(r => r.AtualizarAsync(_pagamento), Times.Once);
    }

    [Fact]
    public async Task Recusado_DeveRecusarComODetalhe()
    {
        NoProvedor(SituacaoNoProvedor.Recusado, "cc_rejected_other_reason");

        await Processar().ProcessarNotificacaoAsync("123");

        _pagamento.Status.Should().Be(StatusPagamento.Recusado);
        _publicador.Verify(p => p.PublicarAsync(It.Is<PagamentoRecusado>(e => e.Motivo == "cc_rejected_other_reason"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecusaAtrasadaDepoisDeConfirmado_DeveSerIgnorada()
    {
        _pagamento.Confirmar();
        NoProvedor(SituacaoNoProvedor.Recusado);

        (await Processar().ProcessarNotificacaoAsync("123")).Should().Be(ResultadoDoProcessamento.Ignorado);

        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AprovadoDepoisDeEncerrada_DeveDevolverSemAvisarASaga(bool recusado)
    {
        if (recusado) _pagamento.Recusar(); else _pagamento.Expirar();
        NoProvedor(SituacaoNoProvedor.Aprovado);

        var resultado = await Processar().ProcessarNotificacaoAsync("123");

        resultado.Should().Be(ResultadoDoProcessamento.Devolvido);
        _gateway.Verify(g => g.EstornarAsync("123", "tardio-123", It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.PublicarAsync(It.IsAny<PagamentoConfirmado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EstornadoNoProvedor_DeveEstornarAqui()
    {
        _pagamento.Confirmar();
        NoProvedor(SituacaoNoProvedor.Estornado);

        await Processar().ProcessarNotificacaoAsync("123");

        _pagamento.Status.Should().Be(StatusPagamento.Estornado);
    }

    [Fact]
    public async Task EmAndamento_NaoDeveMudarNada()
    {
        NoProvedor(SituacaoNoProvedor.EmAndamento);

        (await Processar().ProcessarNotificacaoAsync("123")).Should().Be(ResultadoDoProcessamento.Ignorado);

        _pagamento.Status.Should().Be(StatusPagamento.Pendente);
    }

    [Fact]
    public async Task PagamentoDesconhecidoOuDeOutroSistema_DeveSerIgnorado()
    {
        (await Processar().ProcessarNotificacaoAsync("123")).Should().Be(ResultadoDoProcessamento.Ignorado);

        NoProvedor(SituacaoNoProvedor.Aprovado, referencia: "outro-sistema");
        (await Processar().ProcessarNotificacaoAsync("123")).Should().Be(ResultadoDoProcessamento.Ignorado);

        NoProvedor(SituacaoNoProvedor.Aprovado, referencia: $"{Guid.NewGuid()}:{Guid.NewGuid()}");
        (await Processar().ProcessarNotificacaoAsync("123")).Should().Be(ResultadoDoProcessamento.Ignorado);
    }

    [Fact]
    public async Task Verificar_RecusadoSeguidoDeAprovado_DeveValerOAprovado()
    {
        var referencia = ReferenciaDaCobranca.Montar(_pagamento);
        _gateway.Setup(g => g.BuscarPagamentosAsync(referencia, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PagamentoNoProvedor("1", SituacaoNoProvedor.Recusado, "cc_rejected", referencia),
            new PagamentoNoProvedor("2", SituacaoNoProvedor.Aprovado, "accredited", referencia)
        ]);

        await Processar().VerificarAsync(_pagamento.OrcamentoId, _pagamento.Id);

        _pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        _pagamento.ReferenciaExterna.Should().Be("2");
    }

    [Fact]
    public async Task Verificar_SemPagamentoNoProvedor_DeveSerIgnorado()
    {
        _gateway.Setup(g => g.BuscarPagamentosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        (await Processar().VerificarAsync(_pagamento.OrcamentoId, _pagamento.Id)).Should().Be(ResultadoDoProcessamento.Ignorado);
    }

    [Fact]
    public async Task Verificar_PagamentoInexistente_DeveLancar()
    {
        var acao = () => Processar().VerificarAsync(Guid.NewGuid(), Guid.NewGuid());

        await acao.Should().ThrowAsync<PagamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task InvalidarCobrancas_DeveExpirarSoAsAbertasEFecharNoProvedor()
    {
        var recusado = new Pagamento(_pagamento.OrcamentoId, _pagamento.OrdemDeServicoId, 300m);
        recusado.Recusar();
        _pagamentos.Setup(r => r.ListarPorOrcamentoAsync(_pagamento.OrcamentoId)).ReturnsAsync([_pagamento, recusado]);

        await new InvalidarCobrancasUseCase(_pagamentos.Object, _gateway.Object).ExecutarAsync(_pagamento.OrcamentoId);

        _pagamento.Status.Should().Be(StatusPagamento.Expirado);
        recusado.Status.Should().Be(StatusPagamento.Recusado);
        _gateway.Verify(g => g.ExpirarCobrancaAsync("pref-1", It.IsAny<CancellationToken>()), Times.Once);
        _pagamentos.Verify(r => r.AtualizarAsync(recusado), Times.Never);
    }
}
