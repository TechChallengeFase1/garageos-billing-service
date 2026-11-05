using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.Infrastructure.ExternalServices;

/// <summary>Cobrança pelo Checkout Pro do Mercado Pago</summary>
/// <remarks>
/// O cliente abre o link da preferência e paga na página do Mercado Pago, então
/// nenhum dado de cartão passa por este serviço. O resultado chega pelo webhook.
///
/// binary_mode faz o pagamento ser aprovado ou recusado na hora, sem o estado
/// intermediário "em análise", que deixaria a saga esperando até o prazo.
/// </remarks>
public class MercadoPagoGateway : IGatewayDePagamento
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Sem isto o + do fuso sai escapado, e o Mercado Pago responde error_parsing_date.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly MercadoPagoOptions _opcoes;
    private readonly ILogger<MercadoPagoGateway> _logger;

    public MercadoPagoGateway(HttpClient http, IOptions<MercadoPagoOptions> opcoes, ILogger<MercadoPagoGateway> logger)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task<CobrancaCriada> CriarCobrancaAsync(NovaCobranca cobranca, CancellationToken cancellationToken = default)
    {
        var agora = DateTimeOffset.UtcNow;
        var corpo = new
        {
            Items = new[]
            {
                new { Id = cobranca.Referencia, Title = cobranca.Descricao, Quantity = 1, UnitPrice = cobranca.Valor, CurrencyId = "BRL" }
            },
            ExternalReference = cobranca.Referencia,
            NotificationUrl = string.IsNullOrWhiteSpace(_opcoes.NotificationUrl) ? null : _opcoes.NotificationUrl,
            BinaryMode = true,
            Expires = true,
            ExpirationDateFrom = Data(agora),
            ExpirationDateTo = Data(agora.AddMinutes(_opcoes.ExpiracaoMinutos))
        };

        var resposta = await EnviarAsync(HttpMethod.Post, "checkout/preferences", corpo, null, cancellationToken);
        var preferencia = await LerAsync<PreferenciaDoMercadoPago>(resposta, cancellationToken);

        return new CobrancaCriada(preferencia.Id, preferencia.InitPoint);
    }

    public async Task ExpirarCobrancaAsync(string preferenciaId, CancellationToken cancellationToken = default)
    {
        var corpo = new { Expires = true, ExpirationDateTo = Data(DateTimeOffset.UtcNow) };

        await EnviarAsync(HttpMethod.Put, $"checkout/preferences/{preferenciaId}", corpo, null, cancellationToken);
    }

    public async Task<PagamentoNoProvedor?> ConsultarPagamentoAsync(string pagamentoExternoId, CancellationToken cancellationToken = default)
    {
        var resposta = await EnviarAsync(HttpMethod.Get, $"v1/payments/{Uri.EscapeDataString(pagamentoExternoId)}", null, null, cancellationToken, aceitar404: true);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
            return null;

        return Converter(await LerAsync<PagamentoDoMercadoPago>(resposta, cancellationToken));
    }

    public async Task<IReadOnlyList<PagamentoNoProvedor>> BuscarPagamentosAsync(string referenciaDaCobranca, CancellationToken cancellationToken = default)
    {
        var url = $"v1/payments/search?external_reference={Uri.EscapeDataString(referenciaDaCobranca)}&sort=date_created&criteria=asc";
        var resposta = await EnviarAsync(HttpMethod.Get, url, null, null, cancellationToken);
        var busca = await LerAsync<BuscaDoMercadoPago>(resposta, cancellationToken);

        return busca.Results.Select(Converter).ToList();
    }

    public async Task EstornarAsync(string pagamentoExternoId, string chaveDeIdempotencia, CancellationToken cancellationToken = default)
    {
        await EnviarAsync(HttpMethod.Post, $"v1/payments/{Uri.EscapeDataString(pagamentoExternoId)}/refunds", new { }, chaveDeIdempotencia, cancellationToken);
    }

    private static string Data(DateTimeOffset valor) =>
        valor.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    private static PagamentoNoProvedor Converter(PagamentoDoMercadoPago p) => new(
        p.Id.ToString(CultureInfo.InvariantCulture),
        p.Status switch
        {
            "approved" => SituacaoNoProvedor.Aprovado,
            "rejected" or "cancelled" => SituacaoNoProvedor.Recusado,
            "refunded" or "charged_back" => SituacaoNoProvedor.Estornado,
            _ => SituacaoNoProvedor.EmAndamento
        },
        p.StatusDetail,
        p.ExternalReference);

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string url, object? corpo, string? chaveDeIdempotencia, CancellationToken cancellationToken, bool aceitar404 = false)
    {
        using var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.Authorization = new("Bearer", _opcoes.AccessToken);

        if (corpo is not null)
            pedido.Content = JsonContent.Create(corpo, corpo.GetType(), options: Json);

        if (chaveDeIdempotencia is not null)
            pedido.Headers.Add("X-Idempotency-Key", chaveDeIdempotencia);

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(pedido, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException
                                       or Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new GatewayDePagamentoException("O Mercado Pago não está respondendo. Tente novamente em instantes.", ex);
        }

        if (resposta.IsSuccessStatusCode || (aceitar404 && resposta.StatusCode == HttpStatusCode.NotFound))
            return resposta;

        // Corpo no log para diagnóstico; não traz o token, que vai só no cabeçalho.
        var detalhe = await resposta.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Mercado Pago respondeu {Status} em {Metodo} {Url}: {Detalhe}", (int)resposta.StatusCode, metodo, url, detalhe);
        throw new GatewayDePagamentoException($"O Mercado Pago recusou o pedido (HTTP {(int)resposta.StatusCode}).");
    }

    private static async Task<T> LerAsync<T>(HttpResponseMessage resposta, CancellationToken cancellationToken) =>
        await resposta.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
        ?? throw new GatewayDePagamentoException("O Mercado Pago devolveu uma resposta vazia.");

    private sealed class PreferenciaDoMercadoPago
    {
        public string Id { get; set; } = string.Empty;
        public string InitPoint { get; set; } = string.Empty;
    }

    private sealed class PagamentoDoMercadoPago
    {
        public long Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StatusDetail { get; set; }
        public string? ExternalReference { get; set; }
    }

    private sealed class BuscaDoMercadoPago
    {
        public List<PagamentoDoMercadoPago> Results { get; set; } = [];
    }
}
