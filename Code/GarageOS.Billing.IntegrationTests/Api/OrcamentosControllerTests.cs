using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.IntegrationTests.Fixtures;
using GarageOS.Billing.IntegrationTests.Helpers;

namespace GarageOS.Billing.IntegrationTests.Api;

public class OrcamentosControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public OrcamentosControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static GerarOrcamentoRequest Pedido(Guid? osId = null, Guid? clienteId = null) => new()
    {
        OrdemDeServicoId = osId ?? Guid.NewGuid(),
        ClienteId = clienteId ?? Guid.NewGuid(),
        NumeroOS = "OS-2026-00300",
        Itens =
        [
            new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Servico, ReferenciaId = Guid.NewGuid(), Descricao = "Revisão", Quantidade = 1, ValorUnitario = 180m },
            new ItemOrcamentoRequest { Tipo = TipoItemOrcamento.Peca, ReferenciaId = Guid.NewGuid(), Descricao = "Filtro", Quantidade = 3, ValorUnitario = 20m }
        ]
    };

    private async Task<OrcamentoResponse> GerarAsync(GerarOrcamentoRequest? pedido = null)
    {
        var response = await _client.PostAsJsonAsync("/api/orcamentos", pedido ?? Pedido(), JsonDefaults.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!;
    }

    private Task<HttpResponseMessage> ResponderAsync(Guid id, bool aprovado, Guid? comoCliente = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orcamentos/{id}/resposta")
        {
            Content = JsonContent.Create(new ResponderOrcamentoRequest { Aprovado = aprovado })
        };
        if (comoCliente.HasValue)
            request.Headers.Add("X-Test-Cliente", comoCliente.Value.ToString());
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task POST_Gerar_DeveCalcularTotalEGravarNoDynamo()
    {
        var orcamento = await GerarAsync();

        orcamento.Total.Should().Be(240m);
        orcamento.Status.Should().Be("Pendente");
        orcamento.Itens.Should().HaveCount(2);

        var lido = await _client.GetFromJsonAsync<OrcamentoResponse>($"/api/orcamentos/{orcamento.Id}", JsonDefaults.Options);
        lido!.Total.Should().Be(240m);
        lido.Itens.Select(i => i.Descricao).Should().BeEquivalentTo("Revisão", "Filtro");
    }

    [Fact]
    public async Task POST_Gerar_DuasVezesParaAMesmaOS_DeveDevolverOMesmo()
    {
        var pedido = Pedido();
        var primeiro = await GerarAsync(pedido);

        var segunda = await _client.PostAsJsonAsync("/api/orcamentos", pedido, JsonDefaults.Options);

        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        (await segunda.Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!.Id.Should().Be(primeiro.Id);
    }

    [Fact]
    public async Task POST_Gerar_PedidosSimultaneosParaAMesmaOS_DeveGravarUmSo()
    {
        var pedido = Pedido();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _client.PostAsJsonAsync("/api/orcamentos", pedido, JsonDefaults.Options)));

        respostas.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var ids = await Task.WhenAll(respostas.Select(async r =>
            (await r.Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!.Id));
        ids.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task POST_Gerar_Invalido_DeveRetornar400()
    {
        var response = await _client.PostAsJsonAsync("/api/orcamentos", new GerarOrcamentoRequest(), JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_PorOrdemDeServico_DeveEncontrarPeloMarcador()
    {
        var pedido = Pedido();
        var orcamento = await GerarAsync(pedido);

        var porOs = await _client.GetFromJsonAsync<OrcamentoResponse>($"/api/orcamentos?ordemDeServicoId={pedido.OrdemDeServicoId}", JsonDefaults.Options);
        var semOrcamento = await _client.GetAsync($"/api/orcamentos?ordemDeServicoId={Guid.NewGuid()}");

        porOs!.Id.Should().Be(orcamento.Id);
        semOrcamento.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fluxo_Enviar_ClienteAprova_Cobrar()
    {
        var clienteId = Guid.NewGuid();
        var orcamento = await GerarAsync(Pedido(clienteId: clienteId));

        (await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/enviar", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var resposta = await ResponderAsync(orcamento.Id, aprovado: true, comoCliente: clienteId);
        var cobranca = await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/pagamentos", null);
        var cobrancaDeNovo = await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/pagamentos", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!.Status.Should().Be("Aprovado");
        cobranca.StatusCode.Should().Be(HttpStatusCode.Created);
        cobrancaDeNovo.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagamento = (await cobranca.Content.ReadFromJsonAsync<PagamentoResponse>(JsonDefaults.Options))!;
        pagamento.Valor.Should().Be(240m);
        var lista = await _client.GetFromJsonAsync<List<PagamentoResponse>>($"/api/orcamentos/{orcamento.Id}/pagamentos", JsonDefaults.Options);
        lista.Should().ContainSingle(p => p.Id == pagamento.Id);
        var lido = await _client.GetFromJsonAsync<PagamentoResponse>($"/api/orcamentos/{orcamento.Id}/pagamentos/{pagamento.Id}", JsonDefaults.Options);
        lido!.Status.Should().Be("Criado");
    }

    [Fact]
    public async Task PATCH_Resposta_ClienteDeOutroOrcamento_DeveRetornar403()
    {
        var orcamento = await GerarAsync();
        await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/enviar", null);

        var response = await ResponderAsync(orcamento.Id, aprovado: true, comoCliente: Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetFromJsonAsync<OrcamentoResponse>($"/api/orcamentos/{orcamento.Id}", JsonDefaults.Options))!
            .Status.Should().Be("Enviado");
    }

    [Fact]
    public async Task PATCH_Resposta_AprovarDuasVezes_DeveRetornar409()
    {
        var orcamento = await GerarAsync();
        await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/enviar", null);
        await ResponderAsync(orcamento.Id, aprovado: true);

        var segunda = await ResponderAsync(orcamento.Id, aprovado: true);

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_Cancelar_DeveSerIdempotente()
    {
        var orcamento = await GerarAsync();
        var motivo = new CancelarOrcamentoRequest { Motivo = "Peça indisponível" };

        var primeira = await _client.PostAsJsonAsync($"/api/orcamentos/{orcamento.Id}/cancelar", motivo);
        var segunda = await _client.PostAsJsonAsync($"/api/orcamentos/{orcamento.Id}/cancelar", motivo);
        var semMotivo = await _client.PostAsJsonAsync($"/api/orcamentos/{orcamento.Id}/cancelar", new CancelarOrcamentoRequest());

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        semMotivo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await segunda.Content.ReadFromJsonAsync<OrcamentoResponse>(JsonDefaults.Options))!.MotivoCancelamento.Should().Be("Peça indisponível");
    }

    [Fact]
    public async Task POST_Pagamento_OrcamentoNaoAprovado_DeveRetornar409()
    {
        var orcamento = await GerarAsync();

        var response = await _client.PostAsync($"/api/orcamentos/{orcamento.Id}/pagamentos", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GET_Inexistente_DeveRetornar404()
    {
        (await _client.GetAsync($"/api/orcamentos/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/orcamentos/{Guid.NewGuid()}/pagamentos")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Gerar_ComoCliente_DeveRetornar403()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orcamentos") { Content = JsonContent.Create(Pedido()) };
        request.Headers.Add("X-Test-Cliente", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Health_Ready_DeveEnxergarATabela()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
