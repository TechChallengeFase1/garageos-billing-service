using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GarageOS.Billing.Infrastructure.Persistencia;

/// <summary>Cria a tabela na subida, quando ela não existe</summary>
/// <remarks>
/// Só para o DynamoDB Local (docker compose) e os testes de integração. Na AWS
/// quem cria a tabela é o Terraform (infra/dynamodb.tf), e a role do pod nem
/// tem permissão de CreateTable. A chave e os índices aqui precisam ser os
/// mesmos de lá.
/// </remarks>
public class InicializadorDaTabela : IHostedService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly DynamoDbOptions _opcoes;
    private readonly ILogger<InicializadorDaTabela> _logger;

    public InicializadorDaTabela(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes, ILogger<InicializadorDaTabela> logger)
    {
        _dynamo = dynamo;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_opcoes.CriarTabelaSeAusente)
            return;

        await CriarSeAusenteAsync(_dynamo, _opcoes.TableName, cancellationToken);
        _logger.LogInformation("Tabela {Tabela} pronta", _opcoes.TableName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static async Task CriarSeAusenteAsync(IAmazonDynamoDB dynamo, string tabela, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.DescribeTableAsync(tabela, cancellationToken);
            return;
        }
        catch (ResourceNotFoundException)
        {
            // Segue para criar.
        }

        static GlobalSecondaryIndex Indice(string nome, string hash, string? range) => new()
        {
            IndexName = nome,
            KeySchema = range is null
                ? [new KeySchemaElement(hash, KeyType.HASH)]
                : [new KeySchemaElement(hash, KeyType.HASH), new KeySchemaElement(range, KeyType.RANGE)],
            Projection = new Projection { ProjectionType = ProjectionType.ALL }
        };

        await dynamo.CreateTableAsync(new CreateTableRequest
        {
            TableName = tabela,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            KeySchema =
            [
                new KeySchemaElement(Chaves.PK, KeyType.HASH),
                new KeySchemaElement(Chaves.SK, KeyType.RANGE)
            ],
            AttributeDefinitions =
            [
                new AttributeDefinition(Chaves.PK, ScalarAttributeType.S),
                new AttributeDefinition(Chaves.SK, ScalarAttributeType.S),
                new AttributeDefinition("osId", ScalarAttributeType.S),
                new AttributeDefinition("mpPaymentId", ScalarAttributeType.S),
                new AttributeDefinition("outboxPendente", ScalarAttributeType.S)
            ],
            GlobalSecondaryIndexes =
            [
                Indice(Chaves.IndicePorOs, "osId", Chaves.SK),
                Indice(Chaves.IndicePorPagamentoMp, "mpPaymentId", null),
                Indice(Chaves.IndiceOutboxPendente, "outboxPendente", Chaves.SK)
            ]
        }, cancellationToken);
    }
}
