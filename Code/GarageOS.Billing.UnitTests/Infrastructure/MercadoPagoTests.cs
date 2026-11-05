using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.UnitTests.Infrastructure;

public class MercadoPagoGatewayTests
{
    private sealed class HandlerFalso : HttpMessageHandler
    {
        public HttpRequestMessage? Pedido { get; private set; }
        public string? Corpo { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Resposta { get; set; } = "{}";
        public Exception? Falha { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Falha is not null)
                throw Falha;

            Pedido = request;
            Corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Resposta, Encoding.UTF8, "application/json") };
        }
    }

    private readonly HandlerFalso _handler = new();

    private MercadoPagoGateway Gateway(string? notificationUrl = "https://api.test/webhook") => new(
        new HttpClient(_handler) { BaseAddress = new Uri("https://mp.test/") },
        Options.Create(new MercadoPagoOptions { AccessToken = "TOKEN", NotificationUrl = notificationUrl, ExpiracaoMinutos = 10 }),
        NullLogger<MercadoPagoGateway>.Instance);

    [Fact]
    public async Task CriarCobranca_DeveEnviarOFormatoDoCheckoutPro()
    {
        _handler.Resposta = """{"id":"pref-1","init_point":"https://mp.test/pagar"}""";

        var cobranca = await Gateway().CriarCobrancaAsync(new NovaCobranca("orc:pag", "OS 1", 241.50m));

        cobranca.Should().Be(new CobrancaCriada("pref-1", "https://mp.test/pagar"));
        _handler.Pedido!.Method.Should().Be(HttpMethod.Post);
        _handler.Pedido.RequestUri!.AbsolutePath.Should().Be("/checkout/preferences");
        _handler.Pedido.Headers.Authorization!.ToString().Should().Be("Bearer TOKEN");

        // O Mercado Pago não entende datas com o + escapado: vale o texto cru.
        _handler.Corpo.Should().NotContain(@"\u");
        _handler.Corpo.Should().MatchRegex(@"""expiration_date_to"":""[^""]+\+00:00""");

        var json = JsonDocument.Parse(_handler.Corpo!).RootElement;
        json.GetProperty("external_reference").GetString().Should().Be("orc:pag");
        json.GetProperty("notification_url").GetString().Should().Be("https://api.test/webhook");
        json.GetProperty("binary_mode").GetBoolean().Should().BeTrue();
        json.GetProperty("expires").GetBoolean().Should().BeTrue();
        var item = json.GetProperty("items")[0];
        item.GetProperty("unit_price").GetDecimal().Should().Be(241.50m);
        item.GetProperty("currency_id").GetString().Should().Be("BRL");
        item.GetProperty("quantity").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CriarCobranca_SemUrlDeNotificacao_NaoDeveEnviarOCampo()
    {
        _handler.Resposta = """{"id":"p","init_point":"l"}""";

        await Gateway(notificationUrl: "").CriarCobrancaAsync(new NovaCobranca("r", "d", 1m));

        JsonDocument.Parse(_handler.Corpo!).RootElement.TryGetProperty("notification_url", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExpirarCobranca_DeveAtualizarAPreferencia()
    {
        await Gateway().ExpirarCobrancaAsync("pref-1");

        _handler.Pedido!.Method.Should().Be(HttpMethod.Put);
        _handler.Pedido.RequestUri!.AbsolutePath.Should().Be("/checkout/preferences/pref-1");
        JsonDocument.Parse(_handler.Corpo!).RootElement.GetProperty("expires").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("approved", SituacaoNoProvedor.Aprovado)]
    [InlineData("rejected", SituacaoNoProvedor.Recusado)]
    [InlineData("cancelled", SituacaoNoProvedor.Recusado)]
    [InlineData("refunded", SituacaoNoProvedor.Estornado)]
    [InlineData("charged_back", SituacaoNoProvedor.Estornado)]
    [InlineData("pending", SituacaoNoProvedor.EmAndamento)]
    [InlineData("in_process", SituacaoNoProvedor.EmAndamento)]
    public async Task ConsultarPagamento_DeveConverterOStatus(string status, SituacaoNoProvedor esperado)
    {
        _handler.Resposta = $$"""{"id":123,"status":"{{status}}","status_detail":"d","external_reference":"a:b"}""";

        var pagamento = await Gateway().ConsultarPagamentoAsync("123");

        pagamento.Should().Be(new PagamentoNoProvedor("123", esperado, "d", "a:b"));
        _handler.Pedido!.RequestUri!.AbsolutePath.Should().Be("/v1/payments/123");
    }

    [Fact]
    public async Task ConsultarPagamento_Inexistente_DeveDevolverNull()
    {
        _handler.Status = HttpStatusCode.NotFound;

        (await Gateway().ConsultarPagamentoAsync("9")).Should().BeNull();
    }

    [Fact]
    public async Task BuscarPagamentos_DevePesquisarPelaReferencia()
    {
        _handler.Resposta = """{"results":[{"id":1,"status":"rejected"},{"id":2,"status":"approved"}]}""";

        var pagamentos = await Gateway().BuscarPagamentosAsync("orc:pag");

        pagamentos.Select(p => p.Id).Should().Equal("1", "2");
        _handler.Pedido!.RequestUri!.Query.Should().Contain("external_reference=orc%3Apag");
    }

    [Fact]
    public async Task Estornar_DeveEnviarAChaveDeIdempotencia()
    {
        await Gateway().EstornarAsync("123", "estorno-1");

        _handler.Pedido!.RequestUri!.AbsolutePath.Should().Be("/v1/payments/123/refunds");
        _handler.Pedido.Headers.GetValues("X-Idempotency-Key").Should().ContainSingle("estorno-1");
    }

    [Fact]
    public async Task RecusaDoProvedor_DeveVirarExcecaoSemVazarOToken()
    {
        _handler.Status = HttpStatusCode.BadRequest;

        var acao = () => Gateway().EstornarAsync("123", "k");

        (await acao.Should().ThrowAsync<GatewayDePagamentoException>()).Which.Message.Should().NotContain("TOKEN");
    }

    [Fact]
    public async Task ProvedorFora_DeveVirarExcecaoDeGateway()
    {
        _handler.Falha = new HttpRequestException("sem rede");

        var acao = () => Gateway().ConsultarPagamentoAsync("1");

        await acao.Should().ThrowAsync<GatewayDePagamentoException>();
    }

    [Fact]
    public async Task FakeGateway_DeveAbrirCobrancaSemChamarNinguem()
    {
        var fake = new FakeGatewayDePagamento();

        (await fake.CriarCobrancaAsync(new NovaCobranca("r", "d", 1m))).PreferenciaId.Should().Be("fake-r");
        (await fake.ConsultarPagamentoAsync("1")).Should().BeNull();
        (await fake.BuscarPagamentosAsync("r")).Should().BeEmpty();
        await fake.ExpirarCobrancaAsync("p");
        await fake.EstornarAsync("p", "k");
    }
}

public class ValidadorDeWebhookMercadoPagoTests
{
    private const string Segredo = "segredo";

    private static ValidadorDeWebhookMercadoPago Validador(string segredo = Segredo) =>
        new(Options.Create(new MercadoPagoOptions { WebhookSecret = segredo }));

    private static string Assinar(string texto) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(texto)));

    [Fact]
    public void AssinaturaCorreta_DevePassar()
    {
        var v1 = Assinar("id:123;request-id:req-1;ts:1704908010;");

        Validador().Valido($"ts=1704908010,v1={v1}", "req-1", "123").Should().BeTrue();
    }

    [Fact]
    public void IdAlfanumerico_DeveAssinarEmMinusculas()
    {
        var v1 = Assinar("id:abc;request-id:req-1;ts:1;");

        Validador().Valido($"ts=1,v1={v1}", "req-1", "ABC").Should().BeTrue();
    }

    [Fact]
    public void SemRequestId_DeveTirarOCampoDoTexto()
    {
        var v1 = Assinar("id:123;ts:1;");

        Validador().Valido($"ts=1,v1={v1}", null, "123").Should().BeTrue();
    }

    [Theory]
    [InlineData("999", "req-1")]
    [InlineData("123", "req-2")]
    public void DadosDiferentesDosAssinados_DeveRecusar(string dataId, string requestId)
    {
        var v1 = Assinar("id:123;request-id:req-1;ts:1;");

        Validador().Valido($"ts=1,v1={v1}", requestId, dataId).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("ts=1")]
    [InlineData("v1=abc")]
    [InlineData("ts=1,v1=abc")]
    public void CabecalhoInvalido_DeveRecusar(string? assinatura)
    {
        Validador().Valido(assinatura, "req-1", "123").Should().BeFalse();
    }

    [Fact]
    public void SemSegredoConfigurado_NadaPassa()
    {
        var v1 = Convert.ToHexStringLower(HMACSHA256.HashData(Array.Empty<byte>(), Encoding.UTF8.GetBytes("id:1;ts:1;")));

        Validador(segredo: "").Valido($"ts=1,v1={v1}", null, "1").Should().BeFalse();
    }
}
