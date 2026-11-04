namespace GarageOS.Billing.Application.DTOs.Pagamentos;

/// <summary>Resultado de um pagamento informado pelo provedor (ou simulado)</summary>
public class ResultadoDoPagamentoRequest
{
    public bool Aprovado { get; set; }
    public string? Motivo { get; set; }

    /// <summary>Identificador do pagamento no provedor (Mercado Pago)</summary>
    public string? ReferenciaExterna { get; set; }
}
