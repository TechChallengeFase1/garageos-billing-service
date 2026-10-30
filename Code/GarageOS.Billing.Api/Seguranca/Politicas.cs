namespace GarageOS.Billing.Api.Seguranca;

/// <summary>Nomes das policies de autorização</summary>
public static class Politicas
{
    /// <summary>Equipe da oficina: token do login administrativo, com role Admin</summary>
    public const string Admin = "Admin";

    /// <summary>Cliente final: token emitido pela Lambda a partir do CPF, com a claim cpf</summary>
    public const string Cliente = "Cliente";
}
