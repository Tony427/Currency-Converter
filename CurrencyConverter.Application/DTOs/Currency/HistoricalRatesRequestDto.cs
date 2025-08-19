namespace CurrencyConverter.Application.DTOs.Currency
{
    public class HistoricalRatesRequestDto
    {
        public string BaseCurrency { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
