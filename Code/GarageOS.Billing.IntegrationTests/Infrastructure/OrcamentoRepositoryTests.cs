using FluentAssertions;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.ValueObjects;
using GarageOS.Billing.Infrastructure.Persistencia;
using GarageOS.Billing.Infrastructure.Repositories;
using GarageOS.Billing.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.IntegrationTests.Infrastructure;

public class OrcamentoRepositoryTests : IClassFixture<ApiFactory>
{
    private readonly OrcamentoRepository _orcamentos;
    private readonly PagamentoRepository _pagamentos;

    public OrcamentoRepositoryTests(ApiFactory factory)
    {
        // Sobe a API para que a tabela seja criada pelo inicializador.
        factory.CreateClient();

        var opcoes = Options.Create(new DynamoDbOptions { TableName = ApiFactory.Tabela });
        var cliente = factory.CriarCliente();
        _orcamentos = new OrcamentoRepository(cliente, opcoes);
        _pagamentos = new PagamentoRepository(cliente, opcoes);
    }

    private static Orcamento NovoOrcamento() => new(Guid.NewGuid(), Guid.NewGuid(), "OS-2026-00900",
    [
        new ItemOrcamento(TipoItemOrcamento.Servico, Guid.NewGuid(), "Alinhamento", 1, 120.10m),
        new ItemOrcamento(TipoItemOrcamento.Peca, Guid.NewGuid(), "Pastilha", 4, 33.33m)
    ]);

    [Fact]
    public async Task Adicionar_E_Ler_DevePreservarTodosOsCampos()
    {
        var orcamento = NovoOrcamento();
        orcamento.Enviar();

        await _orcamentos.AdicionarAsync(orcamento);
        var lido = await _orcamentos.ObterPorIdAsync(orcamento.Id);

        lido.Should().BeEquivalentTo(orcamento, o => o
            .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromMilliseconds(1))).WhenTypeIs<DateTime>());
        lido!.Versao.Should().Be(1);
    }

    [Fact]
    public async Task Adicionar_SegundoOrcamentoDaMesmaOS_DeveLancar()
    {
        var primeiro = NovoOrcamento();
        await _orcamentos.AdicionarAsync(primeiro);
        var segundo = new Orcamento(primeiro.OrdemDeServicoId, primeiro.ClienteId, primeiro.NumeroOS, primeiro.Itens);

        var acao = () => _orcamentos.AdicionarAsync(segundo);

        await acao.Should().ThrowAsync<OrcamentoJaExisteException>();
        (await _orcamentos.ObterPorIdAsync(segundo.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Atualizar_ComVersaoDesatualizada_DeveLancarConflito()
    {
        var orcamento = NovoOrcamento();
        await _orcamentos.AdicionarAsync(orcamento);
        var copiaA = (await _orcamentos.ObterPorIdAsync(orcamento.Id))!;
        var copiaB = (await _orcamentos.ObterPorIdAsync(orcamento.Id))!;

        copiaA.Enviar();
        await _orcamentos.AtualizarAsync(copiaA);
        copiaB.Cancelar("Desistência");
        var acao = () => _orcamentos.AtualizarAsync(copiaB);

        await acao.Should().ThrowAsync<ConflitoDeConcorrenciaException>();
        (await _orcamentos.ObterPorIdAsync(orcamento.Id))!.Status.Should().Be(StatusOrcamento.Enviado);
    }

    [Fact]
    public async Task Pagamento_DeveFicarNaParticaoDoOrcamento()
    {
        var orcamento = NovoOrcamento();
        await _orcamentos.AdicionarAsync(orcamento);
        var pagamento = new Pagamento(orcamento.Id, orcamento.OrdemDeServicoId, orcamento.Total);

        await _pagamentos.AdicionarAsync(pagamento);
        pagamento.AguardarPagamento("mp-777");
        await _pagamentos.AtualizarAsync(pagamento);

        var lista = await _pagamentos.ListarPorOrcamentoAsync(orcamento.Id);
        lista.Should().ContainSingle();
        var lido = (await _pagamentos.ObterPorIdAsync(orcamento.Id, pagamento.Id))!;
        lido.Status.Should().Be(StatusPagamento.Pendente);
        lido.ReferenciaExterna.Should().Be("mp-777");
        lido.Versao.Should().Be(2);
    }

    [Fact]
    public async Task AtualizarPagamento_ComVersaoDesatualizada_DeveLancarConflito()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), Guid.NewGuid(), 50m);
        await _pagamentos.AdicionarAsync(pagamento);
        var copia = (await _pagamentos.ObterPorIdAsync(pagamento.OrcamentoId, pagamento.Id))!;

        pagamento.Recusar();
        await _pagamentos.AtualizarAsync(pagamento);
        copia.Expirar();
        var acao = () => _pagamentos.AtualizarAsync(copia);

        await acao.Should().ThrowAsync<ConflitoDeConcorrenciaException>();
    }
}
