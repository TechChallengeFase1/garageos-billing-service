namespace GarageOS.Billing.Application.Abstractions;

/// <summary>Provedor de pagamento</summary>
/// <remarks>
/// Os use cases conhecem só esta interface. A implementação com o Mercado Pago
/// vive na Infrastructure, ao lado de uma falsa para testes e ambiente local.
/// </remarks>
public interface IGatewayDePagamento
{
    /// <summary>Abre a cobrança e devolve o link que o cliente usa para pagar</summary>
    Task<CobrancaCriada> CriarCobrancaAsync(NovaCobranca cobranca, CancellationToken cancellationToken = default);

    /// <summary>Impede que uma cobrança ainda não paga seja paga depois</summary>
    Task ExpirarCobrancaAsync(string preferenciaId, CancellationToken cancellationToken = default);

    /// <returns>O pagamento, ou null se o provedor não o conhece</returns>
    Task<PagamentoNoProvedor?> ConsultarPagamentoAsync(string pagamentoExternoId, CancellationToken cancellationToken = default);

    /// <summary>Pagamentos feitos numa cobrança nossa, para verificar sem depender do webhook</summary>
    Task<IReadOnlyList<PagamentoNoProvedor>> BuscarPagamentosAsync(string referenciaDaCobranca, CancellationToken cancellationToken = default);

    /// <summary>Devolve o valor ao cliente. A chave impede estorno em dobro numa repetição.</summary>
    Task EstornarAsync(string pagamentoExternoId, string chaveDeIdempotencia, CancellationToken cancellationToken = default);
}

public record NovaCobranca(string Referencia, string Descricao, decimal Valor);

public record CobrancaCriada(string PreferenciaId, string LinkPagamento);

public enum SituacaoNoProvedor
{
    EmAndamento,
    Aprovado,
    Recusado,
    Estornado
}

public record PagamentoNoProvedor(string Id, SituacaoNoProvedor Situacao, string? Detalhe, string? Referencia);
