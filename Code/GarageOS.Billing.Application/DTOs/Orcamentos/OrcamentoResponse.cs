namespace GarageOS.Billing.Application.DTOs.Orcamentos;

public class OrcamentoResponse
{
    public Guid Id { get; set; }
    public Guid OrdemDeServicoId { get; set; }
    public Guid ClienteId { get; set; }
    public string NumeroOS { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public DateTime? EnviadoEm { get; set; }
    public DateTime? RespondidoEm { get; set; }
    public string? MotivoCancelamento { get; set; }
    public List<ItemOrcamentoResponse> Itens { get; set; } = [];
}

public class ItemOrcamentoResponse
{
    public string Tipo { get; set; } = string.Empty;
    public Guid ReferenciaId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
