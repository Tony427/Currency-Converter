using FluentValidation;
using CurrencyConverter.Application.DTOs.Currency;
using System;

namespace CurrencyConverter.Application.Validators
{
    public class HistoricalRatesRequestDtoValidator : AbstractValidator<HistoricalRatesRequestDto>
    {
        public HistoricalRatesRequestDtoValidator()
        {
            RuleFor(x => x.BaseCurrency)
                .NotEmpty().WithMessage("BaseCurrency is required.")
                .Length(3).WithMessage("BaseCurrency must be 3 characters long.");

            RuleFor(x => x.FromDate)
                .NotEmpty().WithMessage("FromDate is required.")
                .LessThanOrEqualTo(x => x.ToDate).WithMessage("FromDate cannot be after ToDate.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("FromDate cannot be in the future.");

            RuleFor(x => x.ToDate)
                .NotEmpty().WithMessage("ToDate is required.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("ToDate cannot be in the future.");

            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page must be greater than zero.");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than zero.");
        }
    }
}
