using GarageOS.Billing.Domain.Entities;

namespace GarageOS.Billing.Domain.Repositories;

public interface IOrcamentoRepository
{
    Task<Orcamento?> ObterPorIdAsync(Guid id);
    Task<Orcamento?> ObterPorOrdemDeServicoIdAsync(Guid ordemDeServicoId);

    /// <summary>Grava um orçamento novo</summary>
    /// <exception cref="Exceptions.OrcamentoJaExisteException">A OS já tem orçamento</exception>
    Task AdicionarAsync(Orcamento orcamento);

    /// <summary>Grava as alterações, se ninguém tiver gravado antes</summary>
    /// <exception cref="Exceptions.ConflitoDeConcorrenciaException">A versão gravada mudou</exception>
    Task AtualizarAsync(Orcamento orcamento);
}
