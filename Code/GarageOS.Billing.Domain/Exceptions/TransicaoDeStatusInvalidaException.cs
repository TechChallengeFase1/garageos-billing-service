namespace GarageOS.Billing.Domain.Exceptions;

public class TransicaoDeStatusInvalidaException : Exception
{
    public TransicaoDeStatusInvalidaException(string entidade, string de, string para)
        : base($"Transição de status do {entidade} inválida: {de} -> {para}.") { }

    public TransicaoDeStatusInvalidaException(string mensagem)
        : base(mensagem) { }
}
