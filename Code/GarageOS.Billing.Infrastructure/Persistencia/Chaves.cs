namespace GarageOS.Billing.Infrastructure.Persistencia;

/// <summary>Modelo de chaves da tabela única do Billing (ADR 0007)</summary>
/// <remarks>
/// Uma tabela só, com o tipo do item codificado na chave:
///
///   PK                  SK           Item
///   ORC#{orcamentoId}   META         orçamento, com os itens embutidos
///   ORC#{orcamentoId}   PAG#{id}     pagamento do orçamento
///   OS#{osId}           ORCAMENTO    marcador: "esta OS já tem orçamento"
///
/// O marcador existe para a idempotência. Ele é gravado na mesma transação do
/// orçamento com attribute_not_exists: duas requisições simultâneas para a
/// mesma OS não conseguem gravar dois orçamentos.
/// </remarks>
internal static class Chaves
{
    public const string PK = "PK";
    public const string SK = "SK";

    public const string SkOrcamento = "META";
    public const string SkMarcadorOrcamento = "ORCAMENTO";
    public const string PrefixoPagamento = "PAG#";

    public static string Orcamento(Guid orcamentoId) => $"ORC#{orcamentoId}";
    public static string OrdemDeServico(Guid osId) => $"OS#{osId}";
    public static string Pagamento(Guid pagamentoId) => $"{PrefixoPagamento}{pagamentoId}";

    public const string IndicePorOs = "por-os";
    public const string IndicePorPagamentoMp = "por-pagamento-mp";
    public const string IndiceOutboxPendente = "outbox-pendente";
}
