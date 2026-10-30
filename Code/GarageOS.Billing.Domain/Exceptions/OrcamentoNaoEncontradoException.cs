namespace GarageOS.Billing.Domain.Exceptions;

public class OrcamentoNaoEncontradoException : Exception
{
    public OrcamentoNaoEncontradoException(Guid id)
        : base($"Orçamento '{id}' não encontrado.") { }
}
