using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GarageOS.Billing.IntegrationTests.Auth;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Permite que testes simulem requisição sem JWT (AC10) enviando este header sentinela.
        if (Request.Headers.ContainsKey("X-Test-No-Auth"))
            return Task.FromResult(AuthenticateResult.Fail("No token provided (simulated)."));

        // Com X-Test-Cliente, simula o token do login por CPF: sem role Admin,
        // com a claim cpf e o id do cliente. Sem ele, simula a equipe (Admin).
        var claims = Request.Headers.TryGetValue("X-Test-Cliente", out var clienteId)
            ? new[]
            {
                new Claim(ClaimTypes.NameIdentifier, clienteId.ToString()),
                new Claim("cpf", "12345678909")
            }
            : new[]
            {
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, "Admin")
            };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
