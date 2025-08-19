using Xunit;
using FluentValidation.TestHelper;
using CurrencyConverter.Application.DTOs.Currency;
using CurrencyConverter.Application.Validators;
using System;

namespace CurrencyConverter.Tests.Validators
{
    public class ConversionRequestDtoValidatorTests
    {
        private readonly ConversionRequestDtoValidator _validator;

        public ConversionRequestDtoValidatorTests()
        {
            _validator = new ConversionRequestDtoValidator();
        }

        [Fact]
        public void ShouldHaveError_WhenAmountIsZero()
        {
            var model = new ConversionRequestDto { Amount = 0, FromCurrency = "USD", ToCurrency = "EUR" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Amount);
        }

        [Fact]
        public void ShouldHaveError_WhenAmountIsNegative()
        {
            var model = new ConversionRequestDto { Amount = -10, FromCurrency = "USD", ToCurrency = "EUR" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Amount);
        }

        [Fact]
        public void ShouldNotHaveError_WhenAmountIsPositive()
        {
            var model = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "EUR" };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Amount);
        }

        [Fact]
        public void ShouldHaveError_WhenFromCurrencyIsEmpty()
        {
            var model = new ConversionRequestDto { Amount = 100, FromCurrency = "", ToCurrency = "EUR" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FromCurrency);
        }

        [Fact]
        public void ShouldHaveError_WhenFromCurrencyLengthIsNotThree()
        {
            var model = new ConversionRequestDto { Amount = 100, FromCurrency = "US", ToCurrency = "EUR" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FromCurrency);
        }

        [Fact]
        public void ShouldHaveError_WhenToCurrencyIsEmpty()
        {
            var model = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.ToCurrency);
        }

        [Fact]
        public void ShouldHaveError_WhenToCurrencyLengthIsNotThree()
        {
            var model = new ConversionRequestDto { Amount = 100, FromCurrency = "USD", ToCurrency = "EU" };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.ToCurrency);
        }
    }

    public class HistoricalRatesRequestDtoValidatorTests
    {
        private readonly HistoricalRatesRequestDtoValidator _validator;

        public HistoricalRatesRequestDtoValidatorTests()
        {
            _validator = new HistoricalRatesRequestDtoValidator();
        }

        [Fact]
        public void ShouldHaveError_WhenBaseCurrencyIsEmpty()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
        }

        [Fact]
        public void ShouldHaveError_WhenBaseCurrencyLengthIsNotThree()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "US", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
        }

        [Fact]
        public void ShouldHaveError_WhenFromDateIsEmpty()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FromDate);
        }

        [Fact]
        public void ShouldHaveError_WhenToDateIsEmpty()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.ToDate);
        }

        [Fact]
        public void ShouldHaveError_WhenFromDateIsAfterToDate()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(-1), Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FromDate);
        }

        [Fact]
        public void ShouldHaveError_WhenFromDateIsInFuture()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2), Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.FromDate);
        }

        [Fact]
        public void ShouldHaveError_WhenToDateIsInFuture()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today.AddDays(1), Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.ToDate);
        }

        [Fact]
        public void ShouldHaveError_WhenPageIsZero()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 0, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Page);
        }

        [Fact]
        public void ShouldHaveError_WhenPageIsNegative()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = -1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Page);
        }

        [Fact]
        public void ShouldHaveError_WhenPageSizeIsZero()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 1, PageSize = 0 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.PageSize);
        }

        [Fact]
        public void ShouldHaveError_WhenPageSizeIsNegative()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 1, PageSize = -10 };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.PageSize);
        }

        [Fact]
        public void ShouldNotHaveError_WhenAllFieldsAreValid()
        {
            var model = new HistoricalRatesRequestDto { BaseCurrency = "USD", FromDate = DateTime.Today.AddDays(-1), ToDate = DateTime.Today, Page = 1, PageSize = 10 };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
