using FluentValidation;
using GarageOS.Billing.Application.DTOs.Orcamentos;

namespace GarageOS.Billing.Application.Validators.Orcamentos;

public class CancelarOrcamentoValidator : AbstractValidator<CancelarOrcamentoRequest>
{
    public CancelarOrcamentoValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(500).WithMessage("Motivo deve ter no máximo 500 caracteres.");
    }
}
