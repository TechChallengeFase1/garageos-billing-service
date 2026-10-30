using GarageOS.Billing.Domain.Enums;
using GarageOS.Billing.Domain.Exceptions;
using GarageOS.Billing.Domain.Utils;
using GarageOS.Billing.Domain.ValueObjects;

namespace GarageOS.Billing.Domain.Entities;

/// <summary>Orçamento de uma ordem de serviço</summary>
/// <remarks>
/// Máquina de estados explícita. No monólito Aprovar e Rejeitar não conferiam o
/// status, e aprovar duas vezes dava baixa no estoque duas vezes.
///
///   Pendente -> Enviado   | Cancelado
///   Enviado  -> Aprovado  | Recusado | Cancelado
///   Aprovado -> Cancelado            (compensação: peça em falta, pagamento recusado)
///   Recusado e Cancelado são finais.
/// </remarks>
public class Orcamento
{
    private static readonly Dictionary<StatusOrcamento, StatusOrcamento[]> TransicoesPermitidas = new()
    {
        [StatusOrcamento.Pendente] = [StatusOrcamento.Enviado, StatusOrcamento.Cancelado],
        [StatusOrcamento.Enviado] = [StatusOrcamento.Aprovado, StatusOrcamento.Recusado, StatusOrcamento.Cancelado],
        [StatusOrcamento.Aprovado] = [StatusOrcamento.Cancelado],
        [StatusOrcamento.Recusado] = [],
        [StatusOrcamento.Cancelado] = []
    };

    private readonly List<ItemOrcamento> _itens = [];

    public Guid Id { get; private set; }
    public Guid OrdemDeServicoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public string NumeroOS { get; private set; } = null!;
    public StatusOrcamento Status { get; private set; }
    public decimal Total { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public DateTime? EnviadoEm { get; private set; }
    public DateTime? RespondidoEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Versão para concorrência otimista, controlada pela persistência</summary>
    public long Versao { get; private set; }

    public IReadOnlyCollection<ItemOrcamento> Itens => _itens.AsReadOnly();

    private Orcamento() { }

    public Orcamento(Guid ordemDeServicoId, Guid clienteId, string numeroOS, IEnumerable<ItemOrcamento> itens)
    {
        if (ordemDeServicoId == Guid.Empty)
            throw new ArgumentException("Ordem de serviço é obrigatória.", nameof(ordemDeServicoId));

        if (clienteId == Guid.Empty)
            throw new ArgumentException("Cliente é obrigatório.", nameof(clienteId));

        if (string.IsNullOrWhiteSpace(numeroOS))
            throw new ArgumentException("Número da OS é obrigatório.", nameof(numeroOS));

        _itens.AddRange(itens);
        if (_itens.Count == 0)
            throw new ArgumentException("O orçamento precisa de ao menos um item.", nameof(itens));

        Id = Guid.NewGuid();
        OrdemDeServicoId = ordemDeServicoId;
        ClienteId = clienteId;
        NumeroOS = numeroOS;
        Status = StatusOrcamento.Pendente;

        // Mesma regra do GerarOrcamentoUseCase do monólito (serviços + peças x
        // quantidade), só que sobre os preços copiados no pedido.
        Total = _itens.Sum(i => i.Subtotal);

        CriadoEm = BrasiliaTime.Agora;
        AtualizadoEm = CriadoEm;
    }

    public static bool TransicaoPermitida(StatusOrcamento de, StatusOrcamento para) =>
        TransicoesPermitidas[de].Contains(para);

    public void Enviar()
    {
        MudarPara(StatusOrcamento.Enviado);
        EnviadoEm = AtualizadoEm;
    }

    public void Aprovar()
    {
        MudarPara(StatusOrcamento.Aprovado);
        RespondidoEm = AtualizadoEm;
    }

    public void Recusar()
    {
        MudarPara(StatusOrcamento.Recusado);
        RespondidoEm = AtualizadoEm;
    }

    /// <summary>Cancela o orçamento. Devolve false se ele já estava cancelado.</summary>
    /// <remarks>Idempotente: é uma compensação da saga, e compensação pode chegar repetida.</remarks>
    public bool Cancelar(string motivo)
    {
        if (Status == StatusOrcamento.Cancelado)
            return false;

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Informe o motivo do cancelamento.", nameof(motivo));

        MudarPara(StatusOrcamento.Cancelado);
        MotivoCancelamento = motivo;
        return true;
    }

    private void MudarPara(StatusOrcamento novo)
    {
        if (!TransicaoPermitida(Status, novo))
            throw new TransicaoDeStatusInvalidaException("orçamento", Status.ToString(), novo.ToString());

        Status = novo;
        AtualizadoEm = BrasiliaTime.Agora;
    }

    /// <summary>Reconstrói o orçamento a partir do que está gravado</summary>
    /// <remarks>Uso exclusivo da persistência: não valida transições.</remarks>
    public static Orcamento Restaurar(
        Guid id, Guid ordemDeServicoId, Guid clienteId, string numeroOS, StatusOrcamento status,
        IEnumerable<ItemOrcamento> itens, decimal total, DateTime criadoEm, DateTime atualizadoEm,
        DateTime? enviadoEm, DateTime? respondidoEm, string? motivoCancelamento, long versao)
    {
        var orcamento = new Orcamento
        {
            Id = id,
            OrdemDeServicoId = ordemDeServicoId,
            ClienteId = clienteId,
            NumeroOS = numeroOS,
            Status = status,
            Total = total,
            CriadoEm = criadoEm,
            AtualizadoEm = atualizadoEm,
            EnviadoEm = enviadoEm,
            RespondidoEm = respondidoEm,
            MotivoCancelamento = motivoCancelamento,
            Versao = versao
        };
        orcamento._itens.AddRange(itens);
        return orcamento;
    }

    /// <summary>Atualiza a versão depois de uma gravação bem-sucedida</summary>
    public void DefinirVersao(long versao) => Versao = versao;
}
