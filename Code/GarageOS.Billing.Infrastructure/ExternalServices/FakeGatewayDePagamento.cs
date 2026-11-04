using GarageOS.Billing.Application.Abstractions;

namespace GarageOS.Billing.Infrastructure.ExternalServices;

/// <summary>Provedor falso, para testes e para rodar sem credenciais</summary>
/// <remarks>
/// Ligado com Pagamentos:Provedor=Fake. O resultado do pagamento é informado pela
/// rota de simulação, que só existe neste modo.
/// </remarks>
public class FakeGatewayDePagamento : IGatewayDePagamento
{
    public Task<CobrancaCriada> CriarCobrancaAsync(NovaCobranca cobranca, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CobrancaCriada($"fake-{cobranca.Referencia}", $"https://pagamento.invalid/{cobranca.Referencia}"));

    public Task ExpirarCobrancaAsync(string preferenciaId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<PagamentoNoProvedor?> ConsultarPagamentoAsync(string pagamentoExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PagamentoNoProvedor?>(null);

    public Task<IReadOnlyList<PagamentoNoProvedor>> BuscarPagamentosAsync(string referenciaDaCobranca, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PagamentoNoProvedor>>([]);

    public Task EstornarAsync(string pagamentoExternoId, string chaveDeIdempotencia, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
