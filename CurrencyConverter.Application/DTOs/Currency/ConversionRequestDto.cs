using System;

namespace CurrencyConverter.Application.DTOs.Currency
{
    public class ConversionRequestDto
    {
        public decimal Amount { get; set; }
        public string FromCurrency { get; set; }
        public string ToCurrency { get; set; }
    }
}