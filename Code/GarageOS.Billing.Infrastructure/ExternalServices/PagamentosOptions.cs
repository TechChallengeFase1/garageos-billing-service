namespace GarageOS.Billing.Infrastructure.ExternalServices;

/// <summary>Qual provedor de pagamento usar (seção "Pagamentos")</summary>
public class PagamentosOptions
{
    public const string Secao = "Pagamentos";

    /// <summary>MercadoPago no cluster; Fake nos testes e para rodar sem credenciais</summary>
    public string Provedor { get; set; } = "MercadoPago";

    public bool Falso => string.Equals(Provedor, "Fake", StringComparison.OrdinalIgnoreCase);
}
