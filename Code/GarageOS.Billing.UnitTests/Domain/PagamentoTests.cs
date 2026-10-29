using FluentAssertions;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;

namespace GarageOS.Billing.UnitTests.Domain;

public class PagamentoTests
{
    private static Pagamento Novo() => new(Guid.NewGuid(), Guid.NewGuid(), 241m);

    [Fact]
    public void Construtor_DeveCriarEmAberto()
    {
        var pagamento = Novo();

        pagamento.Status.Should().Be(StatusPagamento.Criado);
        pagamento.EmAberto.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, 0)]
    public void Construtor_Invalido_DeveLancar(bool semOrcamento, decimal valor)
    {
        var acao = () => new Pagamento(semOrcamento ? Guid.Empty : Guid.NewGuid(), Guid.NewGuid(), valor);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CaminhoFeliz_DeveConfirmarEGuardarReferencia()
    {
        var pagamento = Novo();

        pagamento.AguardarPagamento("mp-123");
        pagamento.Confirmar();

        pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        pagamento.ReferenciaExterna.Should().Be("mp-123");
        pagamento.EmAberto.Should().BeTrue();
    }

    [Fact]
    public void AguardarPagamento_SemReferencia_DeveLancar()
    {
        var acao = () => Novo().AguardarPagamento("");

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Estornar_Confirmado_DevePermitirComoCompensacao()
    {
        var pagamento = Novo();
        pagamento.AguardarPagamento("mp-1");
        pagamento.Confirmar();

        pagamento.Estornar();

        pagamento.Status.Should().Be(StatusPagamento.Estornado);
        pagamento.EmAberto.Should().BeFalse();
    }

    [Fact]
    public void Confirmar_SemAguardar_DeveLancar()
    {
        var acao = () => Novo().Confirmar();

        acao.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Theory]
    [InlineData(StatusPagamento.Recusado)]
    [InlineData(StatusPagamento.Expirado)]
    public void Recusar_Expirar_DeveEncerrar(StatusPagamento final)
    {
        var pagamento = Novo();

        if (final == StatusPagamento.Recusado) pagamento.Recusar();
        else pagamento.Expirar();

        pagamento.Status.Should().Be(final);
        pagamento.EmAberto.Should().BeFalse();
        var acao = () => pagamento.Confirmar();
        acao.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Theory]
    [InlineData(StatusPagamento.Criado, StatusPagamento.Pendente, true)]
    [InlineData(StatusPagamento.Criado, StatusPagamento.Confirmado, false)]
    [InlineData(StatusPagamento.Pendente, StatusPagamento.Confirmado, true)]
    [InlineData(StatusPagamento.Pendente, StatusPagamento.Estornado, false)]
    [InlineData(StatusPagamento.Confirmado, StatusPagamento.Estornado, true)]
    [InlineData(StatusPagamento.Estornado, StatusPagamento.Confirmado, false)]
    public void TransicaoPermitida_DeveSeguirATabela(StatusPagamento de, StatusPagamento para, bool esperado)
    {
        Pagamento.TransicaoPermitida(de, para).Should().Be(esperado);
    }

    [Fact]
    public void Restaurar_DeveReconstruir()
    {
        var agora = DateTime.UtcNow;

        var pagamento = Pagamento.Restaurar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m,
            StatusPagamento.Pendente, "mp-9", agora, agora, 3);

        pagamento.Status.Should().Be(StatusPagamento.Pendente);
        pagamento.Versao.Should().Be(3);
        pagamento.DefinirVersao(4);
        pagamento.Versao.Should().Be(4);
    }
}
