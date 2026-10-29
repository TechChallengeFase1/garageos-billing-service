using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Utils;

namespace GarageOS.Billing.Domain.Entities;

/// <summary>Cobrança de um orçamento aprovado</summary>
/// <remarks>
/// A integração com o Mercado Pago entra na etapa 6. Aqui fica o ciclo de vida:
///
///   Criado     -> Pendente   | Recusado | Expirado
///   Pendente   -> Confirmado | Recusado | Expirado
///   Confirmado -> Estornado              (compensação: execução falhou)
/// </remarks>
public class Pagamento
{
    private static readonly Dictionary<StatusPagamento, StatusPagamento[]> TransicoesPermitidas = new()
    {
        [StatusPagamento.Criado] = [StatusPagamento.Pendente, StatusPagamento.Recusado, StatusPagamento.Expirado],
        [StatusPagamento.Pendente] = [StatusPagamento.Confirmado, StatusPagamento.Recusado, StatusPagamento.Expirado],
        [StatusPagamento.Confirmado] = [StatusPagamento.Estornado],
        [StatusPagamento.Recusado] = [],
        [StatusPagamento.Expirado] = [],
        [StatusPagamento.Estornado] = []
    };

    public Guid Id { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public Guid OrdemDeServicoId { get; private set; }
    public decimal Valor { get; private set; }
    public StatusPagamento Status { get; private set; }
    public string? ReferenciaExterna { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public long Versao { get; private set; }

    /// <summary>Ainda pode resultar em dinheiro recebido</summary>
    public bool EmAberto => Status is StatusPagamento.Criado or StatusPagamento.Pendente or StatusPagamento.Confirmado;

    private Pagamento() { }

    public Pagamento(Guid orcamentoId, Guid ordemDeServicoId, decimal valor)
    {
        if (orcamentoId == Guid.Empty)
            throw new ArgumentException("Orçamento é obrigatório.", nameof(orcamentoId));

        if (valor <= 0)
            throw new ArgumentException("Valor deve ser maior que zero.", nameof(valor));

        Id = Guid.NewGuid();
        OrcamentoId = orcamentoId;
        OrdemDeServicoId = ordemDeServicoId;
        Valor = valor;
        Status = StatusPagamento.Criado;
        CriadoEm = BrasiliaTime.Agora;
        AtualizadoEm = CriadoEm;
    }

    public static bool TransicaoPermitida(StatusPagamento de, StatusPagamento para) =>
        TransicoesPermitidas[de].Contains(para);

    /// <summary>A cobrança foi registrada no provedor e aguarda o cliente pagar</summary>
    public void AguardarPagamento(string referenciaExterna)
    {
        if (string.IsNullOrWhiteSpace(referenciaExterna))
            throw new ArgumentException("Referência do provedor é obrigatória.", nameof(referenciaExterna));

        MudarPara(StatusPagamento.Pendente);
        ReferenciaExterna = referenciaExterna;
    }

    public void Confirmar() => MudarPara(StatusPagamento.Confirmado);

    public void Recusar() => MudarPara(StatusPagamento.Recusado);

    public void Expirar() => MudarPara(StatusPagamento.Expirado);

    public void Estornar() => MudarPara(StatusPagamento.Estornado);

    private void MudarPara(StatusPagamento novo)
    {
        if (!TransicaoPermitida(Status, novo))
            throw new TransicaoDeStatusInvalidaException("pagamento", Status.ToString(), novo.ToString());

        Status = novo;
        AtualizadoEm = BrasiliaTime.Agora;
    }

    /// <summary>Reconstrói o pagamento a partir do que está gravado</summary>
    /// <remarks>Uso exclusivo da persistência: não valida transições.</remarks>
    public static Pagamento Restaurar(
        Guid id, Guid orcamentoId, Guid ordemDeServicoId, decimal valor, StatusPagamento status,
        string? referenciaExterna, DateTime criadoEm, DateTime atualizadoEm, long versao) => new()
    {
        Id = id,
        OrcamentoId = orcamentoId,
        OrdemDeServicoId = ordemDeServicoId,
        Valor = valor,
        Status = status,
        ReferenciaExterna = referenciaExterna,
        CriadoEm = criadoEm,
        AtualizadoEm = atualizadoEm,
        Versao = versao
    };

    /// <summary>Atualiza a versão depois de uma gravação bem-sucedida</summary>
    public void DefinirVersao(long versao) => Versao = versao;
}
