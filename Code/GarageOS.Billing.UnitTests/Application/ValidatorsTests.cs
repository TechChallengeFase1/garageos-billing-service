using FluentAssertions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.Validators.Orcamentos;
using GarageOS.Billing.Domain.Enums;

namespace GarageOS.Billing.UnitTests.Application;

public class ValidatorsTests
{
    [Fact]
    public void Gerar_Valido_DevePassar()
    {
        var request = new GerarOrcamentoRequest
        {
            OrdemDeServicoId = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            NumeroOS = "OS-2026-00001",
            Itens = [new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Peca, ReferenciaId = Guid.NewGuid(), Descricao = "Filtro", Quantidade = 1, ValorUnitario = 10m }]
        };

        new GerarOrcamentoValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Gerar_Invalido_DeveApontarCadaErro()
    {
        var request = new GerarOrcamentoRequest
        {
            Itens = [new ItemOrcamentoRequest { Tipo = (TipoItemOrcamento)9, Descricao = "", Quantidade = 0, ValorUnitario = -1 }]
        };

        var erros = new GerarOrcamentoValidator().Validate(request).Errors.Select(e => e.ErrorMessage).ToList();

        erros.Should().Contain([
            "Ordem de serviço é obrigatória.",
            "Cliente é obrigatório.",
            "Número da OS é obrigatório.",
            "Tipo do item deve ser Servico ou Peca.",
            "Referência do item é obrigatória.",
            "Descrição do item é obrigatória.",
            "Quantidade deve ser maior que zero.",
            "Valor unitário não pode ser negativo."
        ]);
    }

    [Fact]
    public void Gerar_SemItens_DeveFalhar()
    {
        var request = new GerarOrcamentoRequest { OrdemDeServicoId = Guid.NewGuid(), ClienteId = Guid.NewGuid(), NumeroOS = "OS-1" };

        new GerarOrcamentoValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Pagamento recusado", true)]
    [InlineData("", false)]
    public void Cancelar_DeveExigirMotivo(string motivo, bool valido)
    {
        new CancelarOrcamentoValidator().Validate(new CancelarOrcamentoRequest { Motivo = motivo }).IsValid.Should().Be(valido);
    }
}
