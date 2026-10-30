using MassTransit;

// ─── Contrato de mensagens da saga da OS (versão 1) ──────────────────────────
//
// Este arquivo é IDÊNTICO nos três serviços (garageos-os-service,
// garageos-billing-service e garageos-execucao-service). Mudou aqui, muda nos
// três, no mesmo PR de cada repositório. Ver ADR 0006.
//
// Cada mensagem declara:
//   - MessageUrn: a identidade do tipo na rede. É o que o MassTransit usa para
//     casar quem publica com quem consome, então o namespace C# poderia até ser
//     diferente entre os repositórios. Não renomeie: renomear é outra mensagem.
//   - EntityName: o nome do tópico SNS. O serviço acrescenta o prefixo do
//     ambiente (garageos-producao-), que é o que a policy IAM de cada pod libera.
//
// Toda mensagem leva o OrdemDeServicoId: é a correlação da saga.
// Datas em UTC; status trafegam como texto, nunca como o número do enum.

namespace GarageOS.Contracts.V1;

/// <summary>Uma linha do pedido de orçamento, com o preço copiado</summary>
public record ItemDoPedido(string Tipo, Guid ReferenciaId, string Descricao, int Quantidade, decimal ValorUnitario);

/// <summary>Uma peça a reservar</summary>
public record PecaDoPedido(Guid EstoqueId, int Quantidade);

/// <summary>Um serviço a executar</summary>
public record ServicoDoPedido(Guid ServicoId, string Nome);

// ─── OS Service ──────────────────────────────────────────────────────────────

[MessageUrn("garageos:os:ordem-de-servico-aberta:v1")]
[EntityName("ordem-de-servico-aberta")]
public record OrdemDeServicoAberta(Guid OrdemDeServicoId, string NumeroOS, Guid ClienteId);

[MessageUrn("garageos:os:orcamento-solicitado:v1")]
[EntityName("orcamento-solicitado")]
public record OrcamentoSolicitado(
    Guid OrdemDeServicoId, string NumeroOS, Guid ClienteId,
    IReadOnlyList<ItemDoPedido> Itens, IReadOnlyList<PecaDoPedido> Pecas, IReadOnlyList<ServicoDoPedido> Servicos);

// ─── Comandos da saga para o Billing ─────────────────────────────────────────

[MessageUrn("garageos:billing:gerar-orcamento:v1")]
[EntityName("gerar-orcamento")]
public record GerarOrcamento(Guid OrdemDeServicoId, string NumeroOS, Guid ClienteId, IReadOnlyList<ItemDoPedido> Itens);

[MessageUrn("garageos:billing:cancelar-orcamento:v1")]
[EntityName("cancelar-orcamento")]
public record CancelarOrcamento(Guid OrdemDeServicoId, string Motivo);

[MessageUrn("garageos:billing:solicitar-pagamento:v1")]
[EntityName("solicitar-pagamento")]
public record SolicitarPagamento(Guid OrdemDeServicoId, Guid OrcamentoId);

[MessageUrn("garageos:billing:estornar-pagamento:v1")]
[EntityName("estornar-pagamento")]
public record EstornarPagamento(Guid OrdemDeServicoId, Guid OrcamentoId, Guid PagamentoId, string Motivo);

// ─── Eventos do Billing ──────────────────────────────────────────────────────

[MessageUrn("garageos:billing:orcamento-enviado:v1")]
[EntityName("orcamento-enviado")]
public record OrcamentoEnviado(Guid OrdemDeServicoId, Guid OrcamentoId, decimal Total);

[MessageUrn("garageos:billing:orcamento-aprovado:v1")]
[EntityName("orcamento-aprovado")]
public record OrcamentoAprovado(Guid OrdemDeServicoId, Guid OrcamentoId);

[MessageUrn("garageos:billing:orcamento-recusado:v1")]
[EntityName("orcamento-recusado")]
public record OrcamentoRecusado(Guid OrdemDeServicoId, Guid OrcamentoId);

[MessageUrn("garageos:billing:orcamento-cancelado:v1")]
[EntityName("orcamento-cancelado")]
public record OrcamentoCancelado(Guid OrdemDeServicoId, Guid OrcamentoId);

[MessageUrn("garageos:billing:pagamento-criado:v1")]
[EntityName("pagamento-criado")]
public record PagamentoCriado(Guid OrdemDeServicoId, Guid OrcamentoId, Guid PagamentoId, decimal Valor);

[MessageUrn("garageos:billing:pagamento-confirmado:v1")]
[EntityName("pagamento-confirmado")]
public record PagamentoConfirmado(Guid OrdemDeServicoId, Guid OrcamentoId, Guid PagamentoId);

[MessageUrn("garageos:billing:pagamento-recusado:v1")]
[EntityName("pagamento-recusado")]
public record PagamentoRecusado(Guid OrdemDeServicoId, Guid OrcamentoId, Guid PagamentoId, string Motivo);

[MessageUrn("garageos:billing:pagamento-estornado:v1")]
[EntityName("pagamento-estornado")]
public record PagamentoEstornado(Guid OrdemDeServicoId, Guid PagamentoId);

// ─── Comandos da saga para o Execução ────────────────────────────────────────

[MessageUrn("garageos:execucao:reservar-pecas:v1")]
[EntityName("reservar-pecas")]
public record ReservarPecas(Guid OrdemDeServicoId, IReadOnlyList<PecaDoPedido> Pecas);

[MessageUrn("garageos:execucao:liberar-reserva-pecas:v1")]
[EntityName("liberar-reserva-pecas")]
public record LiberarReservaPecas(Guid OrdemDeServicoId, string Motivo);

[MessageUrn("garageos:execucao:iniciar-execucao:v1")]
[EntityName("iniciar-execucao")]
public record IniciarExecucao(Guid OrdemDeServicoId, string NumeroOS, IReadOnlyList<ServicoDoPedido> Servicos);

// ─── Eventos do Execução ─────────────────────────────────────────────────────

[MessageUrn("garageos:execucao:pecas-reservadas:v1")]
[EntityName("pecas-reservadas")]
public record PecasReservadas(Guid OrdemDeServicoId);

[MessageUrn("garageos:execucao:pecas-indisponiveis:v1")]
[EntityName("pecas-indisponiveis")]
public record PecasIndisponiveis(Guid OrdemDeServicoId, string Motivo);

[MessageUrn("garageos:execucao:reserva-liberada:v1")]
[EntityName("reserva-liberada")]
public record ReservaLiberada(Guid OrdemDeServicoId);

[MessageUrn("garageos:execucao:execucao-iniciada:v1")]
[EntityName("execucao-iniciada")]
public record ExecucaoIniciada(Guid OrdemDeServicoId, Guid OrdemDeExecucaoId);

[MessageUrn("garageos:execucao:execucao-finalizada:v1")]
[EntityName("execucao-finalizada")]
public record ExecucaoFinalizada(Guid OrdemDeServicoId);

[MessageUrn("garageos:execucao:execucao-falhou:v1")]
[EntityName("execucao-falhou")]
public record ExecucaoFalhou(Guid OrdemDeServicoId, string Motivo);
