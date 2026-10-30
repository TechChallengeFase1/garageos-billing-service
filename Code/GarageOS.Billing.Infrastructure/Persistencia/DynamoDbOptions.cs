namespace GarageOS.Billing.Infrastructure.Persistencia;

/// <summary>Configuração do acesso ao DynamoDB (seção "DynamoDb")</summary>
public class DynamoDbOptions
{
    public const string Secao = "DynamoDb";

    /// <summary>Tabela única do Billing, criada pelo infra/dynamodb.tf</summary>
    public string TableName { get; set; } = "garageos-producao-billing";

    /// <summary>Endpoint alternativo (DynamoDB Local). Vazio = AWS, com a região do ambiente.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Cria a tabela na subida se ela não existir. Só para ambiente local e testes.</summary>
    public bool CriarTabelaSeAusente { get; set; }
}
