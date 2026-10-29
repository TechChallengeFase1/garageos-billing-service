namespace GarageOS.Billing.Domain.Exceptions;

public class PagamentoNaoEncontradoException : Exception
{
    public PagamentoNaoEncontradoException(Guid id)
        : base($"Pagamento '{id}' não encontrado.") { }
}
