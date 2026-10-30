using FluentAssertions;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.Infrastructure.Mensageria.Consumidores;
using GarageOS.Billing.UnitTests.Helpers;
using GarageOS.Contracts.V1;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GarageOS.Billing.UnitTests.Infrastructure;

/// <summary>Consumidores da saga com os use cases de verdade e repositórios em memória</summary>
public class ConsumidoresTests : IAsyncLifetime
{
    private readonly Dictionary<Guid, Orcamento> _porOs = [];
    private readonly Dictionary<Guid, Orcamento> _porId = [];
    private readonly List<Pagamento> _pagamentos = [];
    private readonly Mock<IOrcamentoRepository> _orcamentos = new();
    private readonly Mock<IPagamentoRepository> _repoPagamentos = new();

    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _orcamentos.Setup(r => r.ObterPorOrdemDeServicoIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _porOs.GetValueOrDefault(id));
        _orcamentos.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _porId.GetValueOrDefault(id));
        _orcamentos.Setup(r => r.AdicionarAsync(It.IsAny<Orcamento>())).Callback<Orcamento>(o => { _porOs[o.OrdemDeServicoId] = o; _porId[o.Id] = o; });
        _repoPagamentos.Setup(r => r.ListarPorOrcamentoAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _pagamentos.Where(p => p.OrcamentoId == id).ToList());
        _repoPagamentos.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>())).Callback<Pagamento>(_pagamentos.Add);
        _repoPagamentos.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid o, Guid p) => _pagamentos.FirstOrDefault(x => x.OrcamentoId == o && x.Id == p));

        _provider = new ServiceCollection()
            .AddSingleton(_orcamentos.Object)
            .AddSingleton(_repoPagamentos.Object)
            .AddSingleton(new Mock<IMetricasDeNegocio>().Object)
            .AddScoped<GerarOrcamentoUseCase>()
            .AddScoped<EnviarOrcamentoUseCase>()
            .AddScoped<ObterOrcamentoUseCase>()
            .AddScoped<CancelarOrcamentoUseCase>()
            .AddScoped<CriarPagamentoUseCase>()
            .AddScoped<EstornarPagamentoUseCase>()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<GerarOrcamentoConsumer>();
                x.AddConsumer<CancelarOrcamentoConsumer>();
                x.AddConsumer<SolicitarPagamentoConsumer>();
                x.AddConsumer<EstornarPagamentoConsumer>();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private static GerarOrcamento Pedido(Guid osId) => new(osId, "OS-2026-00050", Guid.NewGuid(),
        [new ItemDoPedido("Servico", Guid.NewGuid(), "Revisão", 1, 200m), new ItemDoPedido("Peca", Guid.NewGuid(), "Filtro", 2, 25m)]);

    [Fact]
    public async Task GerarOrcamento_DeveGerarEnviarEPublicarComOTotal()
    {
        var osId = Guid.NewGuid();

        await _harness.Bus.Publish(Pedido(osId));

        (await _harness.Published.Any<OrcamentoEnviado>(m => m.Context.Message.OrdemDeServicoId == osId && m.Context.Message.Total == 250m)).Should().BeTrue();
        _porOs[osId].Status.Should().Be(StatusOrcamento.Enviado);
    }

    [Fact]
    public async Task GerarOrcamento_Repetido_NaoDeveCriarOutro()
    {
        var osId = Guid.NewGuid();

        await _harness.Bus.Publish(Pedido(osId));
        await _harness.Bus.Publish(Pedido(osId));

        (await _harness.Published.SelectAsync<OrcamentoEnviado>(m => m.Context.Message.OrdemDeServicoId == osId).Count()).Should().Be(2);
        _orcamentos.Verify(r => r.AdicionarAsync(It.IsAny<Orcamento>()), Times.Once);
        (await _harness.Published.Any<Fault>()).Should().BeFalse();
    }

    [Fact]
    public async Task CancelarOrcamento_Aprovado_DeveCancelarEPublicar()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        _porOs[orcamento.OrdemDeServicoId] = orcamento;
        _porId[orcamento.Id] = orcamento;

        await _harness.Bus.Publish(new CancelarOrcamento(orcamento.OrdemDeServicoId, "Peças indisponíveis"));

        (await _harness.Published.Any<OrcamentoCancelado>(m => m.Context.Message.OrcamentoId == orcamento.Id)).Should().BeTrue();
        orcamento.Status.Should().Be(StatusOrcamento.Cancelado);
    }

    [Fact]
    public async Task CancelarOrcamento_SemOrcamentoOuRecusado_DeveAceitarSemErro()
    {
        var recusado = Fabrica.Orcamento();
        recusado.Enviar();
        recusado.Recusar();
        _porOs[recusado.OrdemDeServicoId] = recusado;
        _porId[recusado.Id] = recusado;

        await _harness.Bus.Publish(new CancelarOrcamento(Guid.NewGuid(), "x"));
        await _harness.Bus.Publish(new CancelarOrcamento(recusado.OrdemDeServicoId, "x"));

        (await _harness.Consumed.SelectAsync<CancelarOrcamento>().Count()).Should().Be(2);
        (await _harness.Published.Any<Fault>()).Should().BeFalse();
        recusado.Status.Should().Be(StatusOrcamento.Recusado);
    }

    [Fact]
    public async Task SolicitarPagamento_DeveCriarCobrancaEPublicar()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        _porId[orcamento.Id] = orcamento;

        await _harness.Bus.Publish(new SolicitarPagamento(orcamento.OrdemDeServicoId, orcamento.Id));

        (await _harness.Published.Any<PagamentoCriado>(m => m.Context.Message.Valor == orcamento.Total)).Should().BeTrue();
        _pagamentos.Should().ContainSingle();
    }

    [Fact]
    public async Task EstornarPagamento_Confirmado_DeveEstornarEPublicar()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pagamento.AguardarPagamento("mp-9");
        pagamento.Confirmar();
        _pagamentos.Add(pagamento);

        await _harness.Bus.Publish(new EstornarPagamento(pagamento.OrdemDeServicoId, pagamento.OrcamentoId, pagamento.Id, "Execução falhou"));

        (await _harness.Published.Any<PagamentoEstornado>(m => m.Context.Message.PagamentoId == pagamento.Id)).Should().BeTrue();
        pagamento.Status.Should().Be(StatusPagamento.Estornado);
    }

    [Fact]
    public async Task EstornarPagamento_InexistenteOuNaoConfirmado_DeveAceitarSemErro()
    {
        var criado = new Pagamento(Guid.NewGuid(), Guid.NewGuid(), 100m);
        _pagamentos.Add(criado);

        await _harness.Bus.Publish(new EstornarPagamento(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x"));
        await _harness.Bus.Publish(new EstornarPagamento(criado.OrdemDeServicoId, criado.OrcamentoId, criado.Id, "x"));

        (await _harness.Consumed.SelectAsync<EstornarPagamento>().Count()).Should().Be(2);
        (await _harness.Published.Any<PagamentoEstornado>()).Should().BeFalse();
        (await _harness.Published.Any<Fault>()).Should().BeFalse();
    }
}
