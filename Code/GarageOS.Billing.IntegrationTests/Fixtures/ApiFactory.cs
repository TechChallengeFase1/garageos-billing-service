using Amazon.DynamoDBv2;
using Amazon.Runtime;
using GarageOS.Billing.IntegrationTests.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.DynamoDb;

namespace GarageOS.Billing.IntegrationTests.Fixtures;

/// <summary>API em memória contra um DynamoDB Local em container</summary>
/// <remarks>
/// A tabela é criada na subida pelo mesmo InicializadorDaTabela que o docker
/// compose usa, com a mesma chave e os mesmos índices do infra/dynamodb.tf.
/// </remarks>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Tabela = "garageos-testes-billing";

    private readonly DynamoDbContainer _dynamo = new DynamoDbBuilder("amazon/dynamodb-local:2.5.4").Build();

    public IAmazonDynamoDB CriarCliente() => new AmazonDynamoDBClient(
        new BasicAWSCredentials("test", "test"),
        new AmazonDynamoDBConfig { ServiceURL = _dynamo.GetConnectionString() });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DynamoDb:TableName", Tabela);
        builder.UseSetting("DynamoDb:CriarTabelaSeAusente", "true");

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IAmazonDynamoDB)).ToList())
                services.Remove(descriptor);

            services.AddSingleton(_ => CriarCliente());

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dynamo.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dynamo.DisposeAsync();
        await base.DisposeAsync();
    }
}
