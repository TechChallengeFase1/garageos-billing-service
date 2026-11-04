using GarageOS.Billing.Application.Abstractions;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageOS.Billing.Api.Controllers;

/// <summary>Notificações do provedor de pagamento</summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IValidadorDeWebhook _validador;
    private readonly ProcessarPagamentoDoProvedorUseCase _processar;
    private readonly ILogger<WebhooksController> _logger;

    /// <summary>Inicializa o controller com o validador de assinatura e o processamento</summary>
    public WebhooksController(
        IValidadorDeWebhook validador,
        ProcessarPagamentoDoProvedorUseCase processar,
        ILogger<WebhooksController> logger)
    {
        _validador = validador;
        _processar = processar;
        _logger = logger;
    }

    /// <summary>Recebe as notificações de pagamento do Mercado Pago</summary>
    /// <remarks>
    /// Única rota pública sem JWT: quem chama é o Mercado Pago, e a defesa é a
    /// assinatura x-signature. O Mercado Pago reentrega a notificação até receber
    /// 2xx, e o processamento é idempotente. O resultado vem sempre da consulta à
    /// API dele; o corpo da notificação só indica qual pagamento mudou.
    /// </remarks>
    /// <param name="corpo">Notificação; o id do pagamento também vem na query data.id</param>
    /// <response code="200">Notificação aceita</response>
    /// <response code="401">Assinatura inválida</response>
    /// <response code="503">Mercado Pago indisponível; ele reentrega depois</response>
    [AllowAnonymous]
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> MercadoPago(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] NotificacaoMercadoPago? corpo)
    {
        // A assinatura cobre o data.id da query. Só ele é usado adiante.
        string? dataId = Request.Query["data.id"];
        string? tipo = Request.Query["type"];
        tipo ??= corpo?.Type;

        if (!_validador.Valido(Request.Headers["x-signature"], Request.Headers["x-request-id"], dataId))
        {
            _logger.LogWarning("Notificacao do Mercado Pago recusada: assinatura invalida");
            return Unauthorized();
        }

        if (tipo != "payment" || string.IsNullOrWhiteSpace(dataId))
            return Ok();

        var resultado = await _processar.ProcessarNotificacaoAsync(dataId);
        _logger.LogInformation("Notificacao do pagamento {PagamentoExterno}: {Resultado}", dataId, resultado);

        return Ok();
    }

    /// <summary>Corpo da notificação do Mercado Pago</summary>
    public class NotificacaoMercadoPago
    {
        /// <summary>Tipo da notificação; só "payment" interessa</summary>
        public string? Type { get; set; }
    }
}
