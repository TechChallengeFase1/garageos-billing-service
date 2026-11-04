namespace GarageOS.Billing.Application.DTOs.Pagamentos;

public class PagamentoResponse
{
    public Guid Id { get; set; }
    public Guid OrcamentoId { get; set; }
    public Guid OrdemDeServicoId { get; set; }
    public decimal Valor { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenciaExterna { get; set; }

    /// <summary>Link que o cliente abre para pagar</summary>
    public string? LinkPagamento { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
