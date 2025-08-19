using System;

namespace CurrencyConverter.Application.DTOs.Currency
{
    public class ConversionResponseDto
    {
        public decimal ConvertedAmount { get; set; }
        public string FromCurrency { get; set; }
        public string ToCurrency { get; set; }
        public decimal Rate { get; set; }
        public DateTime Date { get; set; }
    }
}