namespace GarageOS.Billing.Domain.Exceptions;

/// <summary>A OS já tem orçamento</summary>
/// <remarks>
/// Lançada pela persistência quando outra requisição gravou o orçamento da
/// mesma OS no meio do caminho. O use case trata como idempotência e devolve
/// o orçamento que ficou gravado.
/// </remarks>
public class OrcamentoJaExisteException : Exception
{
    public Guid OrdemDeServicoId { get; }

    public OrcamentoJaExisteException(Guid ordemDeServicoId)
        : base($"A OS '{ordemDeServicoId}' já tem orçamento.")
    {
        OrdemDeServicoId = ordemDeServicoId;
    }
}
