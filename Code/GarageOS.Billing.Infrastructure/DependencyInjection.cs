using Amazon.DynamoDBv2;
using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.Infrastructure.Observabilidade;
using GarageOS.Billing.Infrastructure.Persistencia;
using GarageOS.Billing.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GarageOS.Billing.Infrastructure;

/// <summary>Registro da infraestrutura do Billing</summary>
public static class DependencyInjection
{
    /// <summary>Registra o cliente do DynamoDB, os repositórios e as métricas</summary>
    /// <remarks>
    /// Sem chave de acesso em lugar nenhum: o SDK usa a cadeia padrão de
    /// credenciais. No cluster, é a role IRSA da ServiceAccount do pod; no
    /// docker compose, as variáveis AWS_* de mentira que o DynamoDB Local aceita.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var secao = configuration.GetSection(DynamoDbOptions.Secao);
        services.Configure<DynamoDbOptions>(secao);

        var serviceUrl = secao.GetValue<string>(nameof(DynamoDbOptions.ServiceUrl));
        services.AddSingleton<IAmazonDynamoDB>(_ => string.IsNullOrWhiteSpace(serviceUrl)
            ? new AmazonDynamoDBClient()
            : new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl }));

        services.AddHostedService<InicializadorDaTabela>();

        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();

        // Singleton porque nao guarda estado por requisicao e o agente do New
        // Relic e global ao processo.
        services.AddSingleton<IMetricasDeNegocio, MetricasNewRelic>();

        return services;
    }
}
