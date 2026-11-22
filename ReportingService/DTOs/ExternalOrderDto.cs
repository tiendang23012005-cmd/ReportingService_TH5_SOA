using System.Text.Json.Serialization;

namespace ReportingService.DTOs;

public class ExternalOrderDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("items")]
    public List<ExternalOrderItemDto> Items { get; set; } = new();
}