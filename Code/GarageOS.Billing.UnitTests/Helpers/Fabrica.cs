using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.ValueObjects;

namespace GarageOS.Billing.UnitTests.Helpers;

internal static class Fabrica
{
    public static ItemOrcamento Servico(decimal valor = 150m) =>
        new(TipoItemOrcamento.Servico, Guid.NewGuid(), "Troca de óleo", 1, valor);

    public static ItemOrcamento Peca(int quantidade = 2, decimal valor = 45.50m) =>
        new(TipoItemOrcamento.Peca, Guid.NewGuid(), "Filtro de óleo", quantidade, valor);

    public static Orcamento Orcamento(params ItemOrcamento[] itens) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "OS-2026-00007", itens.Length > 0 ? itens : [Servico(), Peca()]);

    public static Orcamento OrcamentoAprovado()
    {
        var orcamento = Orcamento();
        orcamento.Enviar();
        orcamento.Aprovar();
        return orcamento;
    }
}
