using Amazon.SimpleNotificationService;
using Amazon.SQS;
using GarageOS.Billing.Infrastructure.Mensageria.Consumidores;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GarageOS.Billing.Infrastructure.Mensageria;

/// <summary>Registro do MassTransit deste serviço (ADR 0006)</summary>
public static class MensageriaExtensions
{
    /// <summary>Serviço dono das filas: compõe o nome garageos-{ambiente}-billing-*</summary>
    public const string Servico = "billing";

    /// <remarks>
    /// Nomes por convenção, que é o que a policy IAM do pod libera:
    ///   filas   garageos-{ambiente}-billing-{consumidor}  (e as _error, a DLQ do MassTransit)
    ///   tópicos garageos-{ambiente}-{EntityName da mensagem}
    ///
    /// Retry com intervalo crescente antes de mandar para a _error: falha
    /// transitória (banco piscando, outro serviço reiniciando) se resolve
    /// sozinha; o que sobra fica parado na fila de erro, visível no console.
    /// </remarks>
    public static IServiceCollection AddMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(MensageriaOptions.Secao).Get<MensageriaOptions>() ?? new MensageriaOptions();
        services.Configure<MensageriaOptions>(configuration.GetSection(MensageriaOptions.Secao));

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter($"{opcoes.Prefixo}-{Servico}", false));

            x.AddConsumer<GerarOrcamentoConsumer>();
            x.AddConsumer<CancelarOrcamentoConsumer>();
            x.AddConsumer<SolicitarPagamentoConsumer>();
            x.AddConsumer<EstornarPagamentoConsumer>();

            // Sem outbox transacional (o MassTransit não tem um para DynamoDB):
            // o outbox em memória segura o que o consumidor publica e só entrega
            // se ele terminar sem erro. Se falhar, o retry repete tudo, e os
            // use cases idempotentes não duplicam nada no banco.
            x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseInMemoryOutbox(context));

            if (opcoes.EmMemoria)
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000));
                    cfg.ConfigureEndpoints(context);
                });
                return;
            }

            x.UsingAmazonSqs((context, cfg) =>
            {
                cfg.Host(opcoes.Regiao, h =>
                {
                    if (string.IsNullOrWhiteSpace(opcoes.ServiceUrl))
                        return;

                    // LocalStack: mesmo endpoint para SQS e SNS, credencial qualquer.
                    h.Config(new AmazonSQSConfig { ServiceURL = opcoes.ServiceUrl });
                    h.Config(new AmazonSimpleNotificationServiceConfig { ServiceURL = opcoes.ServiceUrl });
                    h.AccessKey("local");
                    h.SecretKey("local");
                });

                cfg.MessageTopology.SetEntityNameFormatter(
                    new PrefixEntityNameFormatter(cfg.MessageTopology.EntityNameFormatter, $"{opcoes.Prefixo}-"));

                cfg.UseMessageRetry(r => r.Intervals(500, 2000, 5000, 15000));
                cfg.ConfigureEndpoints(context);
            });
        });


        services.AddScoped<GarageOS.Billing.Application.Abstractions.IPublicadorDeEventos, PublicadorDeEventos>();

        return services;
    }
}
