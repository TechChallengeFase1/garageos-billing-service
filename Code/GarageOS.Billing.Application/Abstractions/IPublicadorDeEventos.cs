namespace GarageOS.Billing.Application.Abstractions;

/// <summary>Publicação de eventos de integração para os outros serviços</summary>
/// <remarks>
/// Os use cases conhecem só esta interface; o MassTransit e o SQS ficam na
/// Infrastructure, no mesmo padrão do IMetricasDeNegocio.
///
/// O MassTransit não tem outbox transacional para DynamoDB. Por isso toda operação que publica aqui é idempotente: se a publicação falhar depois de gravar, repetir o pedido (ou a mensagem) não muda nada no banco e publica de novo.
/// </remarks>
public interface IPublicadorDeEventos
{
    Task PublicarAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class;
}
