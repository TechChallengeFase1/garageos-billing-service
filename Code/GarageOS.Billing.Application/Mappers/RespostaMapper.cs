using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Domain.Entities;

namespace GarageOS.Billing.Application.Mappers;

/// <summary>Converte entidades em respostas da API, num lugar só</summary>
public static class RespostaMapper
{
    public static OrcamentoResponse ParaResponse(this Orcamento orcamento) => new()
    {
        Id = orcamento.Id,
        OrdemDeServicoId = orcamento.OrdemDeServicoId,
        ClienteId = orcamento.ClienteId,
        NumeroOS = orcamento.NumeroOS,
        Status = orcamento.Status.ToString(),
        Total = orcamento.Total,
        CriadoEm = orcamento.CriadoEm,
        AtualizadoEm = orcamento.AtualizadoEm,
        EnviadoEm = orcamento.EnviadoEm,
        RespondidoEm = orcamento.RespondidoEm,
        MotivoCancelamento = orcamento.MotivoCancelamento,
        Itens = orcamento.Itens.Select(i => new ItemOrcamentoResponse
        {
            Tipo = i.Tipo.ToString(),
            ReferenciaId = i.ReferenciaId,
            Descricao = i.Descricao,
            Quantidade = i.Quantidade,
            ValorUnitario = i.ValorUnitario,
            Subtotal = i.Subtotal
        }).ToList()
    };

    public static PagamentoResponse ParaResponse(this Pagamento pagamento) => new()
    {
        Id = pagamento.Id,
        OrcamentoId = pagamento.OrcamentoId,
        OrdemDeServicoId = pagamento.OrdemDeServicoId,
        Valor = pagamento.Valor,
        Status = pagamento.Status.ToString(),
        ReferenciaExterna = pagamento.ReferenciaExterna,
        CriadoEm = pagamento.CriadoEm,
        AtualizadoEm = pagamento.AtualizadoEm
    };
}
