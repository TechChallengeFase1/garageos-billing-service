using GarageOS.Billing.Domain.Enums;

namespace GarageOS.Billing.Domain.ValueObjects;

/// <summary>Uma linha do orçamento: serviço ou peça, com o preço congelado</summary>
/// <remarks>
/// Os preços chegam copiados no pedido de orçamento. O Billing não consulta o
/// catálogo de serviços (OS Service) nem o estoque (Execução): se o preço mudar
/// depois, o orçamento já enviado ao cliente continua com o valor que ele viu.
/// </remarks>
public sealed record ItemOrcamento
{
    public TipoItemOrcamento Tipo { get; }
    public Guid ReferenciaId { get; }
    public string Descricao { get; }
    public int Quantidade { get; }
    public decimal ValorUnitario { get; }
    public decimal Subtotal => ValorUnitario * Quantidade;

    public ItemOrcamento(TipoItemOrcamento tipo, Guid referenciaId, string descricao, int quantidade, decimal valorUnitario)
    {
        if (referenciaId == Guid.Empty)
            throw new ArgumentException("Referência do item é obrigatória.", nameof(referenciaId));

        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição do item é obrigatória.", nameof(descricao));

        if (quantidade <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));

        if (valorUnitario < 0)
            throw new ArgumentException("Valor unitário não pode ser negativo.", nameof(valorUnitario));

        Tipo = tipo;
        ReferenciaId = referenciaId;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
    }
}
