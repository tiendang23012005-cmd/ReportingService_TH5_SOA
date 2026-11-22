using System.Net.Http.Headers;
using System.Net.Http.Json;
using ReportingService.DTOs;

namespace ReportingService.Services;

public class ExternalServicesClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _context;

    public ExternalServicesClient(
        IHttpClientFactory factory,
        IHttpContextAccessor context)
    {
        _factory = factory;
        _context = context;
    }

    private async Task<List<T>> GetListAsync<T>(
        string serviceName,
        string endpoint)
    {
        var client = _factory.CreateClient(serviceName);

        // Chuyển tiếp JWT từ Client
        var token = _context.HttpContext?
            .Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                AuthenticationHeaderValue.Parse(token);
        }

        var response = await client.GetAsync(endpoint);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"{serviceName} error: {(int)response.StatusCode} - {error}");
        }

        return await response.Content.ReadFromJsonAsync<List<T>>()
               ?? new List<T>();
    }

    // Lấy tất cả sản phẩm
    public Task<List<ExternalProductDto>> GetProductsAsync()
    {
        return GetListAsync<ExternalProductDto>(
            "ProductService", "products");
    }

    // Lấy tất cả đơn hàng
    public Task<List<ExternalOrderDto>> GetOrdersAsync()
    {
        return GetListAsync<ExternalOrderDto>(
            "OrderService", "orders");
    }
}