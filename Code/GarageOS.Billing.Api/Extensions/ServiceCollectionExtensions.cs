using GarageOS.Billing.Api.Middlewares;
using GarageOS.Billing.Api.Seguranca;
using GarageOS.Billing.Api.Saude;
using GarageOS.Billing.Application.UseCases.Orcamentos;
using GarageOS.Billing.Application.UseCases.Pagamentos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

namespace GarageOS.Billing.Api.Extensions;

/// <summary>Extensões de configuração de serviços do Billing Service</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registra todos os use cases e validators da camada de aplicação</summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<GerarOrcamentoUseCase>();
        services.AddScoped<ObterOrcamentoUseCase>();
        services.AddScoped<EnviarOrcamentoUseCase>();
        services.AddScoped<ResponderOrcamentoUseCase>();
        services.AddScoped<CancelarOrcamentoUseCase>();

        services.AddScoped<CriarPagamentoUseCase>();
        services.AddScoped<ConsultarPagamentosUseCase>();
        services.AddScoped<RegistrarResultadoDoPagamentoUseCase>();
        services.AddScoped<EstornarPagamentoUseCase>();

        return services;
    }

    /// <summary>Tag que marca os health checks pertencentes à prontidão (readiness)</summary>
    public const string TagProntidao = "ready";

    /// <summary>Registra os middlewares da aplicação GarageOS</summary>
    /// <remarks>
    /// A ordem importa. O correlation ID vem primeiro para que TUDO abaixo dele
    /// — inclusive o log de erro do ExceptionMiddleware — saia carimbado com o
    /// identificador da requisição.
    /// </remarks>
    public static IApplicationBuilder UseGarageOSMiddlewares(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Uma linha estruturada por requisição, com rota, status e duração.
        // Como roda dentro do escopo do correlation ID, herda o identificador.
        app.UseSerilogRequestLogging();

        app.UseMiddleware<ExceptionMiddleware>();

        return app;
    }

    /// <summary>Registra os health checks de liveness e readiness</summary>
    public static IServiceCollection AddGarageOSHealthChecks(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<DynamoDbHealthCheck>("dynamodb", tags: [TagProntidao]);

        return services;
    }

    /// <summary>Mapeia os endpoints <c>/health/live</c> e <c>/health/ready</c></summary>
    /// <remarks>
    /// A distinção entre os dois é deliberada e importa em produção.
    ///
    /// O liveness NÃO consulta o banco: se o DynamoDB oscilar, o Kubernetes mataria
    /// todos os pods ao mesmo tempo, transformando uma instabilidade de banco
    /// numa queda total da aplicação.
    ///
    /// O readiness consulta: o pod sai do balanceamento enquanto o banco não
    /// responde, e volta sozinho quando ele voltar. Ninguém é reiniciado.
    /// </remarks>
    public static IEndpointRouteBuilder MapGarageOSHealthChecks(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Predicate falso = nenhum check roda. Responde 200 se o processo
            // consegue atender uma requisição HTTP, que é o que liveness mede.
            Predicate = _ => false,
            ResponseWriter = EscreverRespostaHealthAsync
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(TagProntidao),
            ResponseWriter = EscreverRespostaHealthAsync
        });

        return endpoints;
    }

    /// <summary>Serializa o resultado dos health checks em JSON legível</summary>
    private static Task EscreverRespostaHealthAsync(HttpContext context, HealthReport report)
    {
        var resposta = new
        {
            status = report.Status.ToString(),
            duracaoMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entrada => new
            {
                nome = entrada.Key,
                status = entrada.Value.Status.ToString(),
                erro = entrada.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(resposta);
    }

    /// <summary>Configura a autenticação JWT com validação de token</summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var secretKey = configuration["Jwt:SecretKey"]!;
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                                                  Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });

        // Policies por papel. No monólito bastava estar autenticado, então um
        // token de cliente (emitido pela Lambda a partir do CPF) chamava os
        // CRUDs administrativos. Aqui as rotas da oficina exigem Admin.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Politicas.Admin, policy => policy.RequireRole("Admin"));
            options.AddPolicy(Politicas.Cliente, policy => policy.RequireClaim("cpf"));
        });

        return services;
    }

    /// <summary>Configura o Swagger com suporte a autenticação JWT</summary>
    public static IServiceCollection AddSwaggerWithJwt(
        this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "GarageOS Billing API", Version = "v1" });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Informe o token JWT: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (System.IO.File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
