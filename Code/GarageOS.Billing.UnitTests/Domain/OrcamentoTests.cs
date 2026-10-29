using FluentAssertions;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.ValueObjects;
using GarageOS.Billing.UnitTests.Helpers;

namespace GarageOS.Billing.UnitTests.Domain;

public class OrcamentoTests
{
    [Fact]
    public void Construtor_DeveSomarServicosEPecasVezesQuantidade()
    {
        // 150 + 2 x 45,50 = 241
        var orcamento = Fabrica.Orcamento(Fabrica.Servico(150m), Fabrica.Peca(2, 45.50m));

        orcamento.Total.Should().Be(241m);
        orcamento.Status.Should().Be(StatusOrcamento.Pendente);
        orcamento.Itens.Should().HaveCount(2);
    }

    [Fact]
    public void Construtor_SemItens_DeveLancar()
    {
        var acao = () => new Orcamento(Guid.NewGuid(), Guid.NewGuid(), "OS-1", []);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(true, false, "OS-1")]
    [InlineData(false, true, "OS-1")]
    [InlineData(false, false, "")]
    public void Construtor_DadosObrigatorios_DeveLancar(bool semOs, bool semCliente, string numeroOS)
    {
        var acao = () => new Orcamento(
            semOs ? Guid.Empty : Guid.NewGuid(),
            semCliente ? Guid.Empty : Guid.NewGuid(),
            numeroOS,
            [Fabrica.Servico()]);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(StatusOrcamento.Pendente, StatusOrcamento.Enviado, true)]
    [InlineData(StatusOrcamento.Pendente, StatusOrcamento.Cancelado, true)]
    [InlineData(StatusOrcamento.Pendente, StatusOrcamento.Aprovado, false)]
    [InlineData(StatusOrcamento.Enviado, StatusOrcamento.Aprovado, true)]
    [InlineData(StatusOrcamento.Enviado, StatusOrcamento.Recusado, true)]
    [InlineData(StatusOrcamento.Enviado, StatusOrcamento.Cancelado, true)]
    [InlineData(StatusOrcamento.Aprovado, StatusOrcamento.Cancelado, true)]
    [InlineData(StatusOrcamento.Aprovado, StatusOrcamento.Recusado, false)]
    [InlineData(StatusOrcamento.Aprovado, StatusOrcamento.Aprovado, false)]
    [InlineData(StatusOrcamento.Recusado, StatusOrcamento.Cancelado, false)]
    [InlineData(StatusOrcamento.Cancelado, StatusOrcamento.Enviado, false)]
    public void TransicaoPermitida_DeveSeguirATabela(StatusOrcamento de, StatusOrcamento para, bool esperado)
    {
        Orcamento.TransicaoPermitida(de, para).Should().Be(esperado);
    }

    [Fact]
    public void Enviar_Aprovar_DeveRegistrarDatas()
    {
        var orcamento = Fabrica.Orcamento();

        orcamento.Enviar();
        orcamento.Aprovar();

        orcamento.Status.Should().Be(StatusOrcamento.Aprovado);
        orcamento.EnviadoEm.Should().NotBeNull();
        orcamento.RespondidoEm.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_DuasVezes_DeveLancar()
    {
        // No monólito isto passava e dava baixa no estoque em dobro.
        var orcamento = Fabrica.OrcamentoAprovado();

        var acao = () => orcamento.Aprovar();

        acao.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Aprovar_SemEnviar_DeveLancar()
    {
        var acao = () => Fabrica.Orcamento().Aprovar();

        acao.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Recusar_Enviado_DeveFicarRecusado()
    {
        var orcamento = Fabrica.Orcamento();
        orcamento.Enviar();

        orcamento.Recusar();

        orcamento.Status.Should().Be(StatusOrcamento.Recusado);
    }

    [Fact]
    public void Cancelar_Aprovado_DevePermitirComoCompensacao()
    {
        var orcamento = Fabrica.OrcamentoAprovado();

        orcamento.Cancelar("Peça indisponível").Should().BeTrue();

        orcamento.Status.Should().Be(StatusOrcamento.Cancelado);
        orcamento.MotivoCancelamento.Should().Be("Peça indisponível");
    }

    [Fact]
    public void Cancelar_JaCancelado_DeveSerIdempotente()
    {
        var orcamento = Fabrica.Orcamento();
        orcamento.Cancelar("Desistência");

        orcamento.Cancelar("Outro motivo").Should().BeFalse();
        orcamento.MotivoCancelamento.Should().Be("Desistência");
    }

    [Fact]
    public void Cancelar_SemMotivo_DeveLancar()
    {
        var acao = () => Fabrica.Orcamento().Cancelar(" ");

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancelar_Recusado_DeveLancar()
    {
        var orcamento = Fabrica.Orcamento();
        orcamento.Enviar();
        orcamento.Recusar();

        var acao = () => orcamento.Cancelar("Tarde demais");

        acao.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Restaurar_DeveReconstruirSemValidarTransicao()
    {
        var item = Fabrica.Servico(99m);
        var agora = DateTime.UtcNow;

        var orcamento = Orcamento.Restaurar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "OS-9",
            StatusOrcamento.Recusado, [item], 99m, agora, agora, agora, agora, null, 7);

        orcamento.Status.Should().Be(StatusOrcamento.Recusado);
        orcamento.Versao.Should().Be(7);
        orcamento.Itens.Should().ContainSingle().Which.Should().Be(item);
    }

    [Theory]
    [InlineData(true, "Desc", 1, 1)]
    [InlineData(false, "", 1, 1)]
    [InlineData(false, "Desc", 0, 1)]
    [InlineData(false, "Desc", 1, -1)]
    public void ItemOrcamento_Invalido_DeveLancar(bool semReferencia, string descricao, int quantidade, decimal valor)
    {
        var acao = () => new ItemOrcamento(TipoItemOrcamento.Peca, semReferencia ? Guid.Empty : Guid.NewGuid(), descricao, quantidade, valor);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ItemOrcamento_Subtotal_DeveSerValorVezesQuantidade()
    {
        Fabrica.Peca(3, 10m).Subtotal.Should().Be(30m);
    }
}
