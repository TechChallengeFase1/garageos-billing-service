using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;
using static GarageOS.Billing.Infrastructure.Persistencia.Atributos;

namespace GarageOS.Billing.Infrastructure.Repositories;

/// <summary>Pagamentos ficam na mesma partição do orçamento (PK = ORC#id)</summary>
/// <remarks>
/// Listar os pagamentos de um orçamento é uma Query numa partição só, sem
/// índice. O id do Mercado Pago, quando existir (etapa 6), vai no atributo
/// mpPaymentId, que alimenta o índice por-pagamento-mp usado pelo webhook.
/// </remarks>
public class PagamentoRepository : IPagamentoRepository
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;

    public PagamentoRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
    {
        _dynamo = dynamo;
        _tabela = opcoes.Value.TableName;
    }

    public async Task<Pagamento?> ObterPorIdAsync(Guid orcamentoId, Guid pagamentoId)
    {
        var resposta = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = Chave(orcamentoId, pagamentoId),
            ConsistentRead = true
        });

        return resposta.Item is { Count: > 0 } item ? ParaEntidade(item) : null;
    }

    public async Task<IReadOnlyList<Pagamento>> ListarPorOrcamentoAsync(Guid orcamentoId)
    {
        var resposta = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _tabela,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefixo)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = Texto(Chaves.Orcamento(orcamentoId)),
                [":prefixo"] = Texto(Chaves.PrefixoPagamento)
            },
            ConsistentRead = true
        });

        return (resposta.Items ?? []).Select(ParaEntidade).ToList();
    }

    public async Task AdicionarAsync(Pagamento pagamento)
    {
        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _tabela,
            Item = ParaItem(pagamento, versao: 1),
            ConditionExpression = "attribute_not_exists(PK)"
        });

        pagamento.DefinirVersao(1);
    }

    public async Task AtualizarAsync(Pagamento pagamento)
    {
        var novaVersao = pagamento.Versao + 1;

        try
        {
            await _dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = _tabela,
                Item = ParaItem(pagamento, novaVersao),
                ConditionExpression = "#versao = :versaoLida",
                ExpressionAttributeNames = new() { ["#versao"] = "version" },
                ExpressionAttributeValues = new() { [":versaoLida"] = Numero(pagamento.Versao) }
            });
        }
        catch (ConditionalCheckFailedException ex)
        {
            throw new ConflitoDeConcorrenciaException(
                "O pagamento foi alterado por outra requisição ao mesmo tempo. Tente novamente.", ex);
        }

        pagamento.DefinirVersao(novaVersao);
    }

    private static Dictionary<string, AttributeValue> Chave(Guid orcamentoId, Guid pagamentoId) => new()
    {
        [Chaves.PK] = Texto(Chaves.Orcamento(orcamentoId)),
        [Chaves.SK] = Texto(Chaves.Pagamento(pagamentoId))
    };

    private static Dictionary<string, AttributeValue> ParaItem(Pagamento p, long versao)
    {
        var item = Chave(p.OrcamentoId, p.Id);
        item["tipo"] = Texto("Pagamento");
        item["id"] = Texto(p.Id.ToString());
        item["orcamentoId"] = Texto(p.OrcamentoId.ToString());
        item["osId"] = Texto(p.OrdemDeServicoId.ToString());
        item["valor"] = Numero(p.Valor);
        item["status"] = Texto(p.Status.ToString());
        item["criadoEm"] = Data(p.CriadoEm);
        item["atualizadoEm"] = Data(p.AtualizadoEm);
        item["version"] = Numero(versao);
        TextoOpcional(item, "mpPaymentId", p.ReferenciaExterna);
        return item;
    }

    private static Pagamento ParaEntidade(Dictionary<string, AttributeValue> item) => Pagamento.Restaurar(
        item.LerGuid("id"),
        item.LerGuid("orcamentoId"),
        item.LerGuid("osId"),
        item.LerDecimal("valor"),
        item.LerEnum<StatusPagamento>("status"),
        item.LerTextoOpcional("mpPaymentId"),
        item.LerData("criadoEm"),
        item.LerData("atualizadoEm"),
        item.LerLong("version"));
}
