using FluentAssertions;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Contracts.V1;
using Moq;

namespace GarageOS.Billing.UnitTests.Application;

public class ResultadoEEstornoTests
{
    private readonly Mock<IPagamentoRepository> _pagamentos = new();
    private readonly Mock<IPublicadorDeEventos> _publicador = new();
    private readonly Pagamento _pagamento = new(Guid.NewGuid(), Guid.NewGuid(), 300m);

    public ResultadoEEstornoTests()
    {
        _pagamentos.Setup(r => r.ObterPorIdAsync(_pagamento.OrcamentoId, _pagamento.Id)).ReturnsAsync(_pagamento);
    }

    private RegistrarResultadoDoPagamentoUseCase Registrar() => new(_pagamentos.Object, _publicador.Object);

    [Fact]
    public async Task Aprovado_DeveConfirmarEPublicar()
    {
        var resposta = await Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id,
            new ResultadoDoPagamentoRequest { Aprovado = true, ReferenciaExterna = "mp-1" });

        resposta.Status.Should().Be("Confirmado");
        resposta.ReferenciaExterna.Should().Be("mp-1");
        _publicador.Verify(p => p.PublicarAsync(It.Is<PagamentoConfirmado>(e => e.PagamentoId == _pagamento.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recusado_DeveRecusarEPublicarComMotivo()
    {
        await Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id,
            new ResultadoDoPagamentoRequest { Aprovado = false, Motivo = "Saldo insuficiente" });

        _pagamento.Status.Should().Be(StatusPagamento.Recusado);
        _publicador.Verify(p => p.PublicarAsync(It.Is<PagamentoRecusado>(e => e.Motivo == "Saldo insuficiente"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MesmoResultadoRepetido_NaoDeveGravarDeNovo()
    {
        await Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id, new ResultadoDoPagamentoRequest { Aprovado = true });
        await Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id, new ResultadoDoPagamentoRequest { Aprovado = true });

        _pagamentos.Verify(r => r.AtualizarAsync(It.IsAny<Pagamento>()), Times.Once);
        _publicador.Verify(p => p.PublicarAsync(It.IsAny<PagamentoConfirmado>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ResultadoContrario_DeveLancar()
    {
        await Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id, new ResultadoDoPagamentoRequest { Aprovado = true });

        var acao = () => Registrar().ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id, new ResultadoDoPagamentoRequest { Aprovado = false });

        await acao.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public async Task PagamentoInexistente_DeveLancar()
    {
        var acao = () => Registrar().ExecutarAsync(Guid.NewGuid(), Guid.NewGuid(), new ResultadoDoPagamentoRequest { Aprovado = true });

        await acao.Should().ThrowAsync<PagamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task Estornar_Confirmado_DeveEstornarUmaVezSo()
    {
        _pagamento.AguardarPagamento("mp-2");
        _pagamento.Confirmar();
        var useCase = new EstornarPagamentoUseCase(_pagamentos.Object);

        await useCase.ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id);
        var resposta = await useCase.ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id);

        resposta!.Status.Should().Be("Estornado");
        _pagamentos.Verify(r => r.AtualizarAsync(_pagamento), Times.Once);
    }

    [Fact]
    public async Task Estornar_NaoConfirmado_DeveLancar()
    {
        var acao = () => new EstornarPagamentoUseCase(_pagamentos.Object).ExecutarAsync(_pagamento.OrcamentoId, _pagamento.Id);

        await acao.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public async Task Estornar_Inexistente_DeveDevolverNull()
    {
        (await new EstornarPagamentoUseCase(_pagamentos.Object).ExecutarAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeNull();
    }
}
