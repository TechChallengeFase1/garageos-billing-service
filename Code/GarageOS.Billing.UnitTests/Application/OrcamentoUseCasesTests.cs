using FluentAssertions;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.UnitTests.Helpers;
using Moq;

namespace GarageOS.Billing.UnitTests.Application;

public class OrcamentoUseCasesTests
{
    private readonly Mock<IOrcamentoRepository> _repository = new();
    private readonly Mock<IMetricasDeNegocio> _metricas = new();

    private static GerarOrcamentoRequest Pedido(Guid? osId = null) => new()
    {
        OrdemDeServicoId = osId ?? Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        NumeroOS = "OS-2026-00007",
        Itens =
        [
            new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Servico, ReferenciaId = Guid.NewGuid(), Descricao = "Revisão", Quantidade = 1, ValorUnitario = 200m },
            new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Peca, ReferenciaId = Guid.NewGuid(), Descricao = "Filtro", Quantidade = 2, ValorUnitario = 25m }
        ]
    };

    private GerarOrcamentoUseCase Gerar() => new(_repository.Object, _metricas.Object);

    [Fact]
    public async Task Gerar_OSSemOrcamento_DeveCalcularTotalEGravar()
    {
        var (orcamento, criado) = await Gerar().ExecutarAsync(Pedido());

        criado.Should().BeTrue();
        orcamento.Total.Should().Be(250m);
        orcamento.Status.Should().Be("Pendente");
        _repository.Verify(r => r.AdicionarAsync(It.IsAny<Orcamento>()), Times.Once);
        _metricas.Verify(m => m.OrcamentoGerado("OS-2026-00007", 250m), Times.Once);
    }

    [Fact]
    public async Task Gerar_OSComOrcamento_DeveDevolverOExistente()
    {
        var existente = Fabrica.Orcamento();
        _repository.Setup(r => r.ObterPorOrdemDeServicoIdAsync(existente.OrdemDeServicoId)).ReturnsAsync(existente);

        var (orcamento, criado) = await Gerar().ExecutarAsync(Pedido(existente.OrdemDeServicoId));

        criado.Should().BeFalse();
        orcamento.Id.Should().Be(existente.Id);
        _repository.Verify(r => r.AdicionarAsync(It.IsAny<Orcamento>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_CorridaComOutraRequisicao_DeveDevolverOQueFicouGravado()
    {
        var osId = Guid.NewGuid();
        var gravadoPelaOutra = Fabrica.Orcamento();
        _repository.SetupSequence(r => r.ObterPorOrdemDeServicoIdAsync(osId))
            .ReturnsAsync((Orcamento?)null)
            .ReturnsAsync(gravadoPelaOutra);
        _repository.Setup(r => r.AdicionarAsync(It.IsAny<Orcamento>())).ThrowsAsync(new OrcamentoJaExisteException(osId));

        var (orcamento, criado) = await Gerar().ExecutarAsync(Pedido(osId));

        criado.Should().BeFalse();
        orcamento.Id.Should().Be(gravadoPelaOutra.Id);
        _metricas.Verify(m => m.OrcamentoGerado(It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_CorridaSemOrcamentoGravado_DeveLancarConflito()
    {
        var osId = Guid.NewGuid();
        _repository.Setup(r => r.AdicionarAsync(It.IsAny<Orcamento>())).ThrowsAsync(new OrcamentoJaExisteException(osId));

        var acao = () => Gerar().ExecutarAsync(Pedido(osId));

        await acao.Should().ThrowAsync<ConflitoDeConcorrenciaException>();
    }

    [Fact]
    public async Task Obter_Inexistente_DeveLancar()
    {
        var acao = () => new ObterOrcamentoUseCase(_repository.Object).ExecutarAsync(Guid.NewGuid());

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task ObterPorOrdemDeServico_SemOrcamento_DeveDevolverNull()
    {
        var resposta = await new ObterOrcamentoUseCase(_repository.Object).ExecutarPorOrdemDeServicoAsync(Guid.NewGuid());

        resposta.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorOrdemDeServico_ComOrcamento_DeveDevolver()
    {
        var orcamento = Fabrica.Orcamento();
        _repository.Setup(r => r.ObterPorOrdemDeServicoIdAsync(orcamento.OrdemDeServicoId)).ReturnsAsync(orcamento);

        var resposta = await new ObterOrcamentoUseCase(_repository.Object).ExecutarPorOrdemDeServicoAsync(orcamento.OrdemDeServicoId);

        resposta!.Itens.Should().HaveCount(2);
    }

    [Fact]
    public async Task Enviar_DeveGravarEEmitirMetrica()
    {
        var orcamento = Fabrica.Orcamento();
        _repository.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);

        var resposta = await new EnviarOrcamentoUseCase(_repository.Object, _metricas.Object).ExecutarAsync(orcamento.Id);

        resposta.Status.Should().Be("Enviado");
        _repository.Verify(r => r.AtualizarAsync(orcamento), Times.Once);
        _metricas.Verify(m => m.OrcamentoStatusAlterado(orcamento.NumeroOS, "Pendente", "Enviado"), Times.Once);
    }

    [Fact]
    public async Task Enviar_Inexistente_DeveLancar()
    {
        var acao = () => new EnviarOrcamentoUseCase(_repository.Object, _metricas.Object).ExecutarAsync(Guid.NewGuid());

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }

    [Theory]
    [InlineData(true, "Aprovado")]
    [InlineData(false, "Recusado")]
    public async Task Responder_DeveAprovarOuRecusar(bool aprovado, string esperado)
    {
        var orcamento = Fabrica.Orcamento();
        orcamento.Enviar();
        _repository.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);

        var resposta = await new ResponderOrcamentoUseCase(_repository.Object, _metricas.Object)
            .ExecutarAsync(orcamento.Id, new ResponderOrcamentoRequest { Aprovado = aprovado });

        resposta.Status.Should().Be(esperado);
        _repository.Verify(r => r.AtualizarAsync(orcamento), Times.Once);
    }

    [Fact]
    public async Task Responder_JaAprovado_NaoDeveGravar()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        _repository.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);

        var acao = () => new ResponderOrcamentoUseCase(_repository.Object, _metricas.Object)
            .ExecutarAsync(orcamento.Id, new ResponderOrcamentoRequest { Aprovado = true });

        await acao.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        _repository.Verify(r => r.AtualizarAsync(It.IsAny<Orcamento>()), Times.Never);
    }

    [Fact]
    public async Task Responder_Inexistente_DeveLancar()
    {
        var acao = () => new ResponderOrcamentoUseCase(_repository.Object, _metricas.Object)
            .ExecutarAsync(Guid.NewGuid(), new ResponderOrcamentoRequest());

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }

    [Fact]
    public async Task Cancelar_DeveGravarUmaVezSo()
    {
        var orcamento = Fabrica.OrcamentoAprovado();
        _repository.Setup(r => r.ObterPorIdAsync(orcamento.Id)).ReturnsAsync(orcamento);
        var useCase = new CancelarOrcamentoUseCase(_repository.Object, _metricas.Object);

        await useCase.ExecutarAsync(orcamento.Id, new CancelarOrcamentoRequest { Motivo = "Pagamento recusado" });
        var resposta = await useCase.ExecutarAsync(orcamento.Id, new CancelarOrcamentoRequest { Motivo = "Pagamento recusado" });

        resposta.Status.Should().Be("Cancelado");
        _repository.Verify(r => r.AtualizarAsync(orcamento), Times.Once);
    }

    [Fact]
    public async Task Cancelar_Inexistente_DeveLancar()
    {
        var acao = () => new CancelarOrcamentoUseCase(_repository.Object, _metricas.Object)
            .ExecutarAsync(Guid.NewGuid(), new CancelarOrcamentoRequest { Motivo = "x" });

        await acao.Should().ThrowAsync<OrcamentoNaoEncontradoException>();
    }
}
