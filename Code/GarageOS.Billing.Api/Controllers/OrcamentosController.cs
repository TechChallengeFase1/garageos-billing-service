using System.Security.Claims;
using GarageOS.Billing.Api.Seguranca;
using GarageOS.Billing.Application.DTOs.Orcamentos;
using GarageOS.Billing.Application.DTOs.Pagamentos;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using GarageOS.Billing.Application.Validators.Orcamentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageOS.Billing.Api.Controllers;

/// <summary>Orçamentos das ordens de serviço e as cobranças deles</summary>
/// <remarks>
/// Gerar, cancelar e cobrar são passos da saga da OS: na etapa 5 o orquestrador
/// os chama por mensagem. As rotas continuam para operação manual.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrcamentosController : ControllerBase
{
    private readonly GerarOrcamentoUseCase _gerarUseCase;
    private readonly ObterOrcamentoUseCase _obterUseCase;
    private readonly EnviarOrcamentoUseCase _enviarUseCase;
    private readonly ResponderOrcamentoUseCase _responderUseCase;
    private readonly CancelarOrcamentoUseCase _cancelarUseCase;
    private readonly CriarPagamentoUseCase _criarPagamentoUseCase;
    private readonly ConsultarPagamentosUseCase _consultarPagamentosUseCase;

    /// <summary>Inicializa o controller com os use cases de orçamento e pagamento</summary>
    public OrcamentosController(
        GerarOrcamentoUseCase gerarUseCase,
        ObterOrcamentoUseCase obterUseCase,
        EnviarOrcamentoUseCase enviarUseCase,
        ResponderOrcamentoUseCase responderUseCase,
        CancelarOrcamentoUseCase cancelarUseCase,
        CriarPagamentoUseCase criarPagamentoUseCase,
        ConsultarPagamentosUseCase consultarPagamentosUseCase)
    {
        _gerarUseCase = gerarUseCase;
        _obterUseCase = obterUseCase;
        _enviarUseCase = enviarUseCase;
        _responderUseCase = responderUseCase;
        _cancelarUseCase = cancelarUseCase;
        _criarPagamentoUseCase = criarPagamentoUseCase;
        _consultarPagamentosUseCase = consultarPagamentosUseCase;
    }

    /// <summary>Gera o orçamento de uma ordem de serviço</summary>
    /// <remarks>
    /// Recebe os serviços e peças com o preço já definido (copiado do OS Service).
    /// O total é a soma de valor unitário × quantidade.
    /// Idempotente: um orçamento por OS. O pedido repetido devolve o existente com 200.
    /// </remarks>
    /// <response code="201">Orçamento gerado</response>
    /// <response code="200">A OS já tinha orçamento</response>
    /// <response code="400">Dados inválidos</response>
    [HttpPost]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Gerar([FromBody] GerarOrcamentoRequest request)
    {
        var validation = await new GerarOrcamentoValidator().ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var (orcamento, criado) = await _gerarUseCase.ExecutarAsync(request);

        return criado
            ? CreatedAtAction(nameof(Obter), new { id = orcamento.Id }, orcamento)
            : Ok(orcamento);
    }

    /// <summary>Obtém o orçamento de uma ordem de serviço</summary>
    /// <param name="ordemDeServicoId">ID da OS</param>
    /// <response code="200">Orçamento da OS</response>
    /// <response code="404">A OS não tem orçamento</response>
    [HttpGet]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorOrdemDeServico([FromQuery] Guid ordemDeServicoId)
    {
        var orcamento = await _obterUseCase.ExecutarPorOrdemDeServicoAsync(ordemDeServicoId);

        return orcamento is null
            ? NotFound(new { mensagem = $"A OS '{ordemDeServicoId}' não tem orçamento." })
            : Ok(orcamento);
    }

    /// <summary>Obtém um orçamento</summary>
    /// <remarks>A equipe vê qualquer orçamento; o cliente, só os dele.</remarks>
    /// <response code="200">Orçamento encontrado</response>
    /// <response code="403">O orçamento é de outro cliente</response>
    /// <response code="404">Não encontrado</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id)
    {
        var orcamento = await _obterUseCase.ExecutarAsync(id);

        return PodeAcessar(orcamento.ClienteId) ? Ok(orcamento) : Forbid();
    }

    /// <summary>Envia o orçamento ao cliente para aprovação</summary>
    /// <response code="200">Orçamento enviado</response>
    /// <response code="404">Não encontrado</response>
    /// <response code="409">O orçamento não está pendente</response>
    [HttpPost("{id:guid}/enviar")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enviar(Guid id)
    {
        return Ok(await _enviarUseCase.ExecutarAsync(id));
    }

    /// <summary>Aprovação ou recusa do orçamento</summary>
    /// <remarks>
    /// Pelo cliente dono do orçamento (token do login por CPF) ou pela equipe,
    /// registrando a resposta dada no balcão. No monólito esta rota era pública e
    /// não conferia o status: qualquer um aprovava, e aprovar duas vezes dava baixa
    /// em dobro no estoque.
    /// </remarks>
    /// <response code="200">Resposta registrada</response>
    /// <response code="403">O orçamento é de outro cliente</response>
    /// <response code="404">Não encontrado</response>
    /// <response code="409">O orçamento não está aguardando resposta</response>
    [HttpPatch("{id:guid}/resposta")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderOrcamentoRequest request)
    {
        var orcamento = await _obterUseCase.ExecutarAsync(id);
        if (!PodeAcessar(orcamento.ClienteId))
            return Forbid();

        return Ok(await _responderUseCase.ExecutarAsync(id, request));
    }

    /// <summary>Cancela o orçamento</summary>
    /// <remarks>Idempotente. Um orçamento recusado não pode ser cancelado (409).</remarks>
    /// <response code="200">Orçamento cancelado (ou já estava)</response>
    /// <response code="400">Motivo ausente</response>
    /// <response code="404">Não encontrado</response>
    /// <response code="409">O orçamento já foi recusado</response>
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarOrcamentoRequest request)
    {
        var validation = await new CancelarOrcamentoValidator().ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        return Ok(await _cancelarUseCase.ExecutarAsync(id, request));
    }

    /// <summary>Abre a cobrança de um orçamento aprovado</summary>
    /// <remarks>
    /// Idempotente: se já há uma cobrança em aberto, devolve essa com 200.
    /// A integração com o Mercado Pago entra na etapa 6.
    /// </remarks>
    /// <response code="201">Cobrança aberta</response>
    /// <response code="200">Já havia uma cobrança em aberto</response>
    /// <response code="404">Orçamento não encontrado</response>
    /// <response code="409">O orçamento não está aprovado</response>
    [HttpPost("{id:guid}/pagamentos")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(PagamentoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PagamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarPagamento(Guid id)
    {
        var (pagamento, criado) = await _criarPagamentoUseCase.ExecutarAsync(id);

        return criado
            ? CreatedAtAction(nameof(ObterPagamento), new { id, pagamentoId = pagamento.Id }, pagamento)
            : Ok(pagamento);
    }

    /// <summary>Lista as cobranças de um orçamento</summary>
    /// <response code="200">Cobranças do orçamento, da mais antiga para a mais nova</response>
    /// <response code="404">Orçamento não encontrado</response>
    [HttpGet("{id:guid}/pagamentos")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(IEnumerable<PagamentoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarPagamentos(Guid id)
    {
        return Ok(await _consultarPagamentosUseCase.ListarAsync(id));
    }

    /// <summary>Obtém uma cobrança</summary>
    /// <response code="200">Cobrança encontrada</response>
    /// <response code="404">Não encontrada</response>
    [HttpGet("{id:guid}/pagamentos/{pagamentoId:guid}")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(PagamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPagamento(Guid id, Guid pagamentoId)
    {
        return Ok(await _consultarPagamentosUseCase.ObterAsync(id, pagamentoId));
    }

    /// <summary>Admin acessa tudo; o cliente, só o que é dele</summary>
    /// <remarks>
    /// O token do cliente vem da Lambda de login por CPF, com o id do cliente na
    /// claim sub. O handler JWT do ASP.NET Core a renomeia para NameIdentifier.
    /// </remarks>
    private bool PodeAcessar(Guid clienteId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return User.HasClaim(c => c.Type == "cpf") && Guid.TryParse(sub, out var id) && id == clienteId;
    }
}
