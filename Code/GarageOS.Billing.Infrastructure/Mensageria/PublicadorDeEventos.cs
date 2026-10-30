using GarageOS.Billing.Application.Abstractions;
using MassTransit;

namespace GarageOS.Billing.Infrastructure.Mensageria;

/// <summary>Publica eventos pelo MassTransit</summary>
public class PublicadorDeEventos : IPublicadorDeEventos
{
    private readonly IPublishEndpoint _publish;

    public PublicadorDeEventos(IPublishEndpoint publish)
    {
        _publish = publish;
    }

    public Task PublicarAsync<T>(T mensagem, CancellationToken cancellationToken = default) where T : class =>
        _publish.Publish(mensagem, cancellationToken);
}
