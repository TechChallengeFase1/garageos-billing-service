namespace GarageOS.Billing.Infrastructure.Mensageria;

/// <summary>Configuração da mensageria (seção "Mensageria")</summary>
public class MensageriaOptions
{
    public const string Secao = "Mensageria";

    /// <summary>AmazonSqs no cluster; InMemory nos testes e para rodar um serviço sozinho</summary>
    public string Transporte { get; set; } = "AmazonSqs";

    /// <summary>Ambiente que compõe o prefixo das filas e tópicos (garageos-producao-*)</summary>
    public string Ambiente { get; set; } = "producao";

    public string Regiao { get; set; } = "us-east-1";

    /// <summary>Endpoint alternativo de SQS e SNS (LocalStack). Vazio = AWS.</summary>
    public string? ServiceUrl { get; set; }

    public string Prefixo => $"garageos-{Ambiente}";

    public bool EmMemoria => string.Equals(Transporte, "InMemory", StringComparison.OrdinalIgnoreCase);
}
