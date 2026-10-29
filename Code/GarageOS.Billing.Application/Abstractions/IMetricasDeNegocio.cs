namespace GarageOS.Billing.Application.Abstractions;

/// <summary>
/// Publica eventos de negócio para a ferramenta de observabilidade.
/// </summary>
/// <remarks>
/// Existe como interface na camada de Application para que os use cases não
/// conheçam o fornecedor. A implementação com New Relic vive na Infrastructure.
/// </remarks>
public interface IMetricasDeNegocio
{
    /// <summary>Registra a geração de um orçamento</summary>
    void OrcamentoGerado(string numeroOS, decimal total);

    /// <summary>Registra a mudança de status de um orçamento</summary>
    void OrcamentoStatusAlterado(string numeroOS, string statusAnterior, string statusNovo);
}
