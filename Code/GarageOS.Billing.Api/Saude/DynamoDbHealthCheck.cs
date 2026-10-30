using Amazon.DynamoDBv2;
using GarageOS.Billing.Infrastructure.Persistencia;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.Api.Saude;

/// <summary>Prontidão: a tabela do Billing responde</summary>
/// <remarks>
/// DescribeTable em vez de uma leitura: confirma credencial (IRSA), rede e a
/// existência da tabela sem consumir capacidade de leitura.
/// </remarks>
public class DynamoDbHealthCheck : IHealthCheck
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;

    /// <summary>Inicializa o health check com o cliente e o nome da tabela</summary>
    public DynamoDbHealthCheck(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
    {
        _dynamo = dynamo;
        _tabela = opcoes.Value.TableName;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var resposta = await _dynamo.DescribeTableAsync(_tabela, cancellationToken);
            return resposta.Table.TableStatus == "ACTIVE"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Tabela {_tabela} em {resposta.Table.TableStatus}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Tabela {_tabela} indisponível", ex);
        }
    }
}
