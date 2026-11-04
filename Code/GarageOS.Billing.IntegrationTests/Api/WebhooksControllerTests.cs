using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.IntegrationTests.Fixtures;
using GarageOS.Billing.IntegrationTests.Helpers;

namespace GarageOS.Billing.IntegrationTests.Api;

public class WebhooksControllerTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public WebhooksControllerTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string Assinatura(string dataId, string requestId, string ts = "1704908010")
    {
        var texto = $"id:{dataId};request-id:{requestId};ts:{ts};";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiFactory.SegredoDoWebhook), Encoding.UTF8.GetBytes(texto));
        return $"ts={ts},v1={Convert.ToHexStringLower(hash)}";
    }

    private Task<HttpResponseMessage> NotificarAsync(string dataId, string? assinatura, string tipo = "payment")
    {
        // Sem token de propósito: o Mercado Pago não manda JWT.
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}&type={tipo}")
        {
            Content = JsonContent.Create(new { type = tipo, data = new { id = dataId } })
        };
        request.Headers.Add("X-Test-No-Auth", "1");
        request.Headers.Add("x-request-id", "req-1");
        if (assinatura is not null)
            request.Headers.Add("x-signature", assinatura);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task SemAssinatura_DeveRetornar401()
    {
        (await NotificarAsync("123", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssinaturaDeOutroPagamento_DeveRetornar401()
    {
        (await NotificarAsync("999", Assinatura("123", "req-1"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssinaturaValida_DeveRetornar200SemToken()
    {
        (await NotificarAsync("123", Assinatura("123", "req-1"))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TipoQueNaoEPagamento_DeveRetornar200SemProcessar()
    {
        (await NotificarAsync("123", Assinatura("123", "req-1"), tipo: "merchant_order")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SimularEVerificar_ComProvedorFalso_DevemFuncionar()
    {
        var pedido = new GerarOrcamentoRequest
        {
            OrdemDeServicoId = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            NumeroOS = "OS-2026-00400",
            Itens = [new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Servico, ReferenciaId = Guid.NewGuid(), Descricao = "Revisão", Quantidade = 1, ValorUnitario = 100m }]
        };
        var orcamento = (await (await _client.PostAsJsonAsync("/api/orcamentos", pedido, JsonDefaults.Options)).Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!;
        await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/enviar", null);
        await _client.PatchAsJsonAsync($"/api/orcamentos/{orcamento.Id}/resposta", new ResponderOrcamentoRequest { Aprovado = true });
        var pagamento = (await (await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/pagamentos", null)).Content.ReadFromJsonAsync<PagamentoResponse>(JsonDefaults.Options))!;

        var verificar = await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/pagamentos/{pagamento.Id}/verificar", null);
        var simular = await _client.PostAsJsonAsync($"/api/orcamentos/{orcamento.Id}/pagamentos/{pagamento.Id}/simular", new ResultadoDoPagamentoRequest { Aprovado = true });

        verificar.StatusCode.Should().Be(HttpStatusCode.OK);
        simular.StatusCode.Should().Be(HttpStatusCode.OK);
        (await simular.Content.ReadFromJsonAsync<PagamentoResponse>(JsonDefaults.Options))!.Status.Should().Be("Confirmado");
    }
}
