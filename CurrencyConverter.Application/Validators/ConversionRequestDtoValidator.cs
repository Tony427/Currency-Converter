using FluentValidation;
using CurrencyConverter.Application.DTOs.Currency;

namespace CurrencyConverter.Application.Validators
{
    public class ConversionRequestDtoValidator : AbstractValidator<ConversionRequestDto>
    {
        public ConversionRequestDtoValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than zero.");

            RuleFor(x => x.FromCurrency)
                .NotEmpty().WithMessage("FromCurrency is required.")
                .Length(3).WithMessage("FromCurrency must be 3 characters long.");

            RuleFor(x => x.ToCurrency)
                .NotEmpty().WithMessage("ToCurrency is required.")
                .Length(3).WithMessage("ToCurrency must be 3 characters long.");
        }
    }
}
