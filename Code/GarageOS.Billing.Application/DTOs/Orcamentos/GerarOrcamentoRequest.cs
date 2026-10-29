using GarageOS.Billing.Domain.Enums;

namespace GarageOS.Billing.Application.DTOs.Orcamentos;

public class GerarOrcamentoRequest
{
    public Guid OrdemDeServicoId { get; set; }
    public Guid ClienteId { get; set; }
    public string NumeroOS { get; set; } = string.Empty;
    public List<ItemOrcamentoRequest> Itens { get; set; } = [];
}

public class ItemOrcamentoRequest
{
    public TipoItemOrcamento Tipo { get; set; }
    public Guid ReferenciaId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
}
