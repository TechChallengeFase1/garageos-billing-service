using FluentValidation;
using GarageOS.Billing.Application.DTOs.Orcamentos;

namespace GarageOS.Billing.Application.Validators.Orcamentos;

public class GerarOrcamentoValidator : AbstractValidator<GerarOrcamentoRequest>
{
    public GerarOrcamentoValidator()
    {
        RuleFor(x => x.OrdemDeServicoId)
            .NotEmpty().WithMessage("Ordem de serviço é obrigatória.");

        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("Cliente é obrigatório.");

        RuleFor(x => x.NumeroOS)
            .NotEmpty().WithMessage("Número da OS é obrigatório.")
            .MaximumLength(20).WithMessage("Número da OS deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Informe ao menos um item.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Tipo)
                .IsInEnum().WithMessage("Tipo do item deve ser Servico ou Peca.");

            item.RuleFor(i => i.ReferenciaId)
                .NotEmpty().WithMessage("Referência do item é obrigatória.");

            item.RuleFor(i => i.Descricao)
                .NotEmpty().WithMessage("Descrição do item é obrigatória.")
                .MaximumLength(150).WithMessage("Descrição do item deve ter no máximo 150 caracteres.");

            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");

            item.RuleFor(i => i.ValorUnitario)
                .GreaterThanOrEqualTo(0).WithMessage("Valor unitário não pode ser negativo.");
        });
    }
}
