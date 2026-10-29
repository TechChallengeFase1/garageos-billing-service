namespace GarageOS.Billing.Domain.Exceptions;

/// <summary>Outra requisição alterou o mesmo registro ao mesmo tempo</summary>
/// <remarks>
/// Lançada pela persistência quando a versão gravada não é a que foi lida.
/// Nada foi gravado; quem chamou pode tentar de novo.
/// </remarks>
public class ConflitoDeConcorrenciaException : Exception
{
    public ConflitoDeConcorrenciaException(string mensagem, Exception? interna = null)
        : base(mensagem, interna) { }
}
