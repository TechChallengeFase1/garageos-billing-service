using GarageOS.Billing.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GarageOS.Billing.Infrastructure.Observabilidade;

/// <summary>Publica os eventos de negócio como custom events do New Relic</summary>
/// <remarks>
/// Nenhuma chamada aqui lança: observabilidade quebrada não pode derrubar uma
/// cobrança. Falha vira log e a operação segue.
/// </remarks>
public class MetricasNewRelic : IMetricasDeNegocio
{
    private readonly ILogger<MetricasNewRelic> _logger;

    /// <summary>Inicializa a publicação de métricas com o logger de diagnóstico</summary>
    public MetricasNewRelic(ILogger<MetricasNewRelic> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OrcamentoGerado(string numeroOS, decimal total) =>
        Registrar("OrcamentoGerado", new Dictionary<string, object>
        {
            ["numeroOS"] = numeroOS,
            ["total"] = (double)total
        });

    /// <inheritdoc />
    public void OrcamentoStatusAlterado(string numeroOS, string statusAnterior, string statusNovo) =>
        Registrar("OrcamentoStatus", new Dictionary<string, object>
        {
            ["numeroOS"] = numeroOS,
            ["statusAnterior"] = statusAnterior,
            ["statusNovo"] = statusNovo
        });

    private void Registrar(string evento, Dictionary<string, object> atributos)
    {
        try
        {
            NewRelic.Api.Agent.NewRelic.RecordCustomEvent(evento, atributos);
        }
        catch (Exception ex)
        {
            // Sem agente configurado (ambiente local, por exemplo) isto apenas
            // não faz nada. Nunca propaga.
            _logger.LogDebug(ex, "Nao foi possivel registrar o evento {Evento}", evento);
        }
    }
}
