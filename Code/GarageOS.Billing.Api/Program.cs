using System.Text.Json.Serialization;
using GarageOS.Billing.Api.Extensions;
using GarageOS.Billing.Infrastructure;
using GarageOS.Billing.Infrastructure.Mensageria;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ─── Logs estruturados em JSON ───────────────────────────────────────────────
// O CompactJsonFormatter grava uma linha JSON por evento no stdout, que é de
// onde o Kubernetes e o agente do New Relic coletam.
//
// Enrich.FromLogContext() não é opcional: sem ele o CorrelationIdMiddleware
// empurra a propriedade e ela simplesmente não aparece em lugar nenhum.
builder.Services.AddSerilog((servicos, configuracao) => configuracao
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .ReadFrom.Services(servicos)
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSwaggerWithJwt();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMensageria(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddGarageOSHealthChecks();

var app = builder.Build();

// Swagger por flag, e não por ambiente: no cluster ele pode ser ligado sem
// arrastar junto o comportamento de Development.
if (builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GarageOS Billing API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseGarageOSMiddlewares();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGarageOSHealthChecks();

await app.RunAsync();

// Necessário para WebApplicationFactory nos testes de integração
/// <summary>Entry point do Billing Service</summary>
public partial class Program
{
    /// <summary>Construtor protegido para uso pelo WebApplicationFactory nos testes</summary>
    protected Program() { }
}
