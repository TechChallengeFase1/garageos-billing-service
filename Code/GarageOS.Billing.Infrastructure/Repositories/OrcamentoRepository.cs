using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GarageOS.Billing.Domain.Entities;
using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Repositories;
using GarageOS.Billing.Domain.ValueObjects;
using GarageOS.Billing.Infrastructure.Persistencia;
using Microsoft.Extensions.Options;
using static GarageOS.Billing.Infrastructure.Persistencia.Atributos;

namespace GarageOS.Billing.Infrastructure.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;

    public OrcamentoRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> opcoes)
    {
        _dynamo = dynamo;
        _tabela = opcoes.Value.TableName;
    }

    public async Task<Orcamento?> ObterPorIdAsync(Guid id)
    {
        var resposta = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = ChaveDoOrcamento(id),
            ConsistentRead = true
        });

        return resposta.Item is { Count: > 0 } item ? ParaEntidade(item) : null;
    }

    public async Task<Orcamento?> ObterPorOrdemDeServicoIdAsync(Guid ordemDeServicoId)
    {
        var marcador = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = ChaveDoMarcador(ordemDeServicoId),
            ConsistentRead = true
        });

        if (marcador.Item is not { Count: > 0 })
            return null;

        return await ObterPorIdAsync(marcador.Item.LerGuid("orcamentoId"));
    }

    /// <remarks>
    /// Orçamento e marcador da OS na mesma transação, ambos com
    /// attribute_not_exists. Se a OS já tem orçamento, nada é gravado.
    /// </remarks>
    public async Task AdicionarAsync(Orcamento orcamento)
    {
        var marcador = ChaveDoMarcador(orcamento.OrdemDeServicoId);
        marcador["orcamentoId"] = Texto(orcamento.Id.ToString());

        try
        {
            await _dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tabela,
                            Item = marcador,
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tabela,
                            Item = ParaItem(orcamento, versao: 1),
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    }
                ]
            });
        }
        catch (TransactionCanceledException ex)
            when (ex.CancellationReasons.Any(r => r.Code == "ConditionalCheckFailed"))
        {
            throw new OrcamentoJaExisteException(orcamento.OrdemDeServicoId);
        }

        orcamento.DefinirVersao(1);
    }

    /// <remarks>
    /// Concorrência otimista: só grava se a versão no banco ainda for a que foi
    /// lida. Duas respostas simultâneas do cliente não se sobrescrevem.
    /// </remarks>
    public async Task AtualizarAsync(Orcamento orcamento)
    {
        var novaVersao = orcamento.Versao + 1;

        try
        {
            await _dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = _tabela,
                Item = ParaItem(orcamento, novaVersao),
                ConditionExpression = "#versao = :versaoLida",
                ExpressionAttributeNames = new() { ["#versao"] = "version" },
                ExpressionAttributeValues = new() { [":versaoLida"] = Numero(orcamento.Versao) }
            });
        }
        catch (ConditionalCheckFailedException ex)
        {
            throw new ConflitoDeConcorrenciaException(
                "O orçamento foi alterado por outra requisição ao mesmo tempo. Tente novamente.", ex);
        }

        orcamento.DefinirVersao(novaVersao);
    }

    private static Dictionary<string, AttributeValue> ChaveDoOrcamento(Guid id) => new()
    {
        [Chaves.PK] = Texto(Chaves.Orcamento(id)),
        [Chaves.SK] = Texto(Chaves.SkOrcamento)
    };

    private static Dictionary<string, AttributeValue> ChaveDoMarcador(Guid osId) => new()
    {
        [Chaves.PK] = Texto(Chaves.OrdemDeServico(osId)),
        [Chaves.SK] = Texto(Chaves.SkMarcadorOrcamento)
    };

    private static Dictionary<string, AttributeValue> ParaItem(Orcamento o, long versao)
    {
        var item = ChaveDoOrcamento(o.Id);
        item["tipo"] = Texto("Orcamento");
        item["id"] = Texto(o.Id.ToString());
        item["osId"] = Texto(o.OrdemDeServicoId.ToString());
        item["clienteId"] = Texto(o.ClienteId.ToString());
        item["numeroOS"] = Texto(o.NumeroOS);
        item["status"] = Texto(o.Status.ToString());
        item["total"] = Numero(o.Total);
        item["criadoEm"] = Data(o.CriadoEm);
        item["atualizadoEm"] = Data(o.AtualizadoEm);
        item["version"] = Numero(versao);
        DataOpcional(item, "enviadoEm", o.EnviadoEm);
        DataOpcional(item, "respondidoEm", o.RespondidoEm);
        TextoOpcional(item, "motivoCancelamento", o.MotivoCancelamento);
        item["itens"] = new AttributeValue
        {
            L = o.Itens.Select(i => new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["tipo"] = Texto(i.Tipo.ToString()),
                    ["referenciaId"] = Texto(i.ReferenciaId.ToString()),
                    ["descricao"] = Texto(i.Descricao),
                    ["quantidade"] = Numero(i.Quantidade),
                    ["valorUnitario"] = Numero(i.ValorUnitario)
                }
            }).ToList()
        };
        return item;
    }

    private static Orcamento ParaEntidade(Dictionary<string, AttributeValue> item)
    {
        var itens = item["itens"].L.Select(a => new ItemOrcamento(
            a.M.LerEnum<TipoItemOrcamento>("tipo"),
            a.M.LerGuid("referenciaId"),
            a.M.LerTexto("descricao"),
            (int)a.M.LerLong("quantidade"),
            a.M.LerDecimal("valorUnitario")));

        return Orcamento.Restaurar(
            item.LerGuid("id"),
            item.LerGuid("osId"),
            item.LerGuid("clienteId"),
            item.LerTexto("numeroOS"),
            item.LerEnum<StatusOrcamento>("status"),
            itens,
            item.LerDecimal("total"),
            item.LerData("criadoEm"),
            item.LerData("atualizadoEm"),
            item.LerDataOpcional("enviadoEm"),
            item.LerDataOpcional("respondidoEm"),
            item.LerTextoOpcional("motivoCancelamento"),
            item.LerLong("version"));
    }
}
