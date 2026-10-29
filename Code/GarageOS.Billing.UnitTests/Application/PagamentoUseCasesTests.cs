using FluentAssertions;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.UnitTests.Helpers;
using Moq;

namespace GarageOS.Billing.UnitTests.Application;

public class PagamentoUseCasesTests
{
    private readonly Mock<IOrcamentoRepository> _orcamentos = new();
    private readonly Mock<IPagamentoRepository> _pagamentos = new();

    private CriarPagamentoUseCase Criar() => new(_orcamentos.Object, _pagamentos.Object);
    private ConsultarPagamentosUseCase Consultar() => new(_orcamentos.Object, _pagamentos.Object);

    [Fact]
    public async Task Criar_OrcamentoAprovado_DeveCobrarOTotal()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        _orcamentos.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);
        _pagamentos.Setup(r => r.ListarPorOrcamentoAsync(orcamento.Id)).ReturnsAsync([]);

        var (pagamento, criado) = await Criar().ExecutarAsync(orcamento.Id);

        criado.Should().BeTrue();
        pagamento.Valor.Should().Be(orcamento.Total);
        pagamento.Status.Should().Be("Criado");
        _pagamentos.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>()), Times.Once);
    }

    [Fact]
    public async Task Criar_ComCobrancaEmAberto_DeveDevolverAExistente()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        var existente = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, orcamento.Total);
        _orcamentos.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);
        _pagamentos.Setup(r => r.ListarPorOrcamentoAsync(orcamento.Id)).ReturnsAsync([existente]);

        var (pagamento, criado) = await Criar().ExecutarAsync(orcamento.Id);

        criado.Should().BeFalse();
        pagamento.Id.Should().Be(existente.Id);
        _pagamentos.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>()), Times.Never);
    }

    [Fact]
    public async Task Criar_AnteriorRecusado_DeveAbrirNovaCobranca()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        var recusado = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, orcamento.Total);
        recusado.Recusar();
        _orcamentos.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);
        _pagamentos.Setup(r => r.ListarPorOrcamentoAsync(orcamento.Id)).ReturnsAsync([recusado]);

        var (_, criado) = await Criar().ExecutarAsync(orcamento.Id);

        criado.Should().BeTrue();
    }

    [Fact]
    public async Task Criar_OrcamentoNaoAprovado_DeveLancar()
    {
        var orcamento = Fabrica.Orcamento();
        _orcamentos.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);

        var acao = () => Criar().ExecutarAsync(orcamento.Id);

        await acao.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public async Task Criar_OrcamentoInexistente_DeveLancar()
    {
        var acao = () => Criar().ExecutarAsync(Guid.NewGuid());

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task Listar_DeveOrdenarPorCriacao()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        var primeiro = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, 10m);
        primeiro.Recusar();
        var segundo = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, 10m);
        _orcamentos.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);
        _pagamentos.Setup(r => r.ListarPorOrcamentoAsync(orcamento.Id)).ReturnsAsync([segundo, primeiro]);

        var lista = (await Consultar().ListarAsync(orcamento.Id)).ToList();

        lista.Select(p => p.Id).Should().Equal(primeiro.Id, segundo.Id);
    }

    [Fact]
    public async Task Listar_OrcamentoInexistente_DeveLancar()
    {
        var acao = () => Consultar().ListarAsync(Guid.NewGuid());

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task Obter_Existente_DeveDevolver()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), Guid.NewGuid(), 10m);
        _pagamentos.Setup(r => r.ObterPorIdAsync(pagamento.OrcamentoId, pagamento.Id)).ReturnsAsync(pagamento);

        var resposta = await Consultar().ObterAsync(pagamento.OrcamentoId, pagamento.Id);

        resposta.Id.Should().Be(pagamento.Id);
    }

    [Fact]
    public async Task Obter_Inexistente_DeveLancar()
    {
        var acao = () => Consultar().ObterAsync(Guid.NewGuid(), Guid.NewGuid());

        await acao.Should().ThrowAsync<PagamentoNaoEncontradoException>();
    }
}
