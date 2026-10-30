using GarageOS.Billing.Domain.Entities;

namespace GarageOS.Billing.Domain.Repositories;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid orcamentoId, Guid pagamentoId);
    Task<IReadOnlyList<Pagamento>> ListarPorOrcamentoAsync(Guid orcamentoId);
    Task AdicionarAsync(Pagamento pagamento);

    /// <exception cref="Exceptions.ConflitoDeConcorrenciaException">A versão gravada mudou</exception>
    Task AtualizarAsync(Pagamento pagamento);
}
