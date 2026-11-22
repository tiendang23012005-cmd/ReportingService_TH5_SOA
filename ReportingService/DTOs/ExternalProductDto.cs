using System.Text.Json.Serialization;

namespace ReportingService.DTOs;

public class ExternalProductDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("cost")]
    public decimal? Cost { get; set; }
}