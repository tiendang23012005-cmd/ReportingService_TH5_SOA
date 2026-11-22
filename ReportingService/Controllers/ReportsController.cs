using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

using ReportingService.Data;
using ReportingService.Models;
using ReportingService.Services;
using ReportingService.DTOs;

namespace ReportingService.Controllers;

[ApiController]
[Route("reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly ExternalServicesClient _external;

    public ReportsController(
        ReportingDbContext db,
        ExternalServicesClient external)
    {
        _db = db;
        _external = external;
    }

    // GET /reports/orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrderReports()
    {
        return Ok(await _db.OrderReports
            .OrderByDescending(x => x.Id)
            .ToListAsync());
    }

    // GET /reports/orders/{id}
    // id ở đây là order_id
    [HttpGet("orders/{id}")]
    public async Task<IActionResult> GetOrderReport(int id)
    {
        var report = await _db.OrderReports
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (report == null)
            return NotFound("Order report not found");

        return Ok(report);
    }

    // POST /reports/orders
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrderReports()
    {
        try
        {
            var orders = await _external.GetOrdersAsync();
            var products = await _external.GetProductsAsync();

            var productMap = products.ToDictionary(x => x.Id);

            // Kiểm tra dữ liệu giá nhập trước khi tính báo cáo
            foreach (var order in orders.Where(x =>
                x.Status.Equals("completed",
                    StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var item in order.Items)
                {
                    if (!productMap.ContainsKey(item.ProductId))
                        return BadRequest(
                            $"Product {item.ProductId} not found");

                    if (productMap[item.ProductId].Cost == null)
                        return BadRequest(
                            $"Product {item.ProductId} has no import cost");
                }
            }

            foreach (var order in orders)
            {
                decimal revenue = 0;
                decimal cost = 0;

                bool completed = order.Status.Equals(
                    "completed",
                    StringComparison.OrdinalIgnoreCase);

                if (completed)
                {
                    foreach (var item in order.Items)
                    {
                        var product = productMap[item.ProductId];

                        revenue += item.TotalPrice != 0
                            ? item.TotalPrice
                            : item.UnitPrice * item.Quantity;

                        cost += product.Cost!.Value * item.Quantity;
                    }
                }

                var report = await _db.OrderReports
                    .FirstOrDefaultAsync(x => x.OrderId == order.Id);

                if (report == null)
                {
                    report = new OrderReport
                    {
                        OrderId = order.Id
                    };

                    _db.OrderReports.Add(report);
                }

                report.TotalRevenue = revenue;
                report.TotalCost = cost;
                report.TotalProfit = revenue - cost;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Order reports generated successfully",
                totalOrders = orders.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, ex.Message);
        }
    }

    // DELETE /reports/orders/{id}
    // id ở đây là order_id
    [HttpDelete("orders/{id}")]
    public async Task<IActionResult> DeleteOrderReport(int id)
    {
        var report = await _db.OrderReports
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (report == null)
            return NotFound("Order report not found");

        _db.OrderReports.Remove(report);

        await _db.SaveChangesAsync();

        return Ok("Order report deleted successfully");
    }

    // GET /reports/products
    [HttpGet("products")]
    public async Task<IActionResult> GetProductReports()
    {
        var reports = await _db.ProductReports
            .AsNoTracking()
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                StockQuantity = g.Max(x => x.StockQuantity),
                TotalSold = g.Sum(x => x.TotalSold),
                Revenue = g.Sum(x => x.Revenue),
                Cost = g.Sum(x => x.Cost),
                Profit = g.Sum(x => x.Profit)
            })
            .ToListAsync();

        return Ok(reports);
    }

    // GET /reports/products/{id}
    // id ở đây là product_id
    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProductReport(int id)
    {
        var reports = await _db.ProductReports
            .Where(x => x.ProductId == id)
            .ToListAsync();

        if (!reports.Any())
            return NotFound("Product report not found");

        return Ok(new
        {
            ProductId = id,
            StockQuantity = reports.Max(x => x.StockQuantity),
            TotalSold = reports.Sum(x => x.TotalSold),
            Revenue = reports.Sum(x => x.Revenue),
            Cost = reports.Sum(x => x.Cost),
            Profit = reports.Sum(x => x.Profit)
        });
    }

    // POST /reports/products
    [HttpPost("products")]
    public async Task<IActionResult> CreateProductReports()
    {
        try
        {
            var products = await _external.GetProductsAsync();
            var orders = await _external.GetOrdersAsync();

            // Phải tạo báo cáo đơn hàng trước
            var orderReports = await _db.OrderReports.ToListAsync();

            var orderReportMap = orderReports.ToDictionary(
                x => x.OrderId, x => x.Id);

            // Xóa báo cáo sản phẩm cũ để tránh trùng lặp
            var oldReports = await _db.ProductReports.ToListAsync();

            _db.ProductReports.RemoveRange(oldReports);

            var newReports = new List<ProductReport>();

            foreach (var product in products)
            {
                var sales = orders
                    .Where(o => o.Status.Equals(
                        "completed",
                        StringComparison.OrdinalIgnoreCase))
                    .SelectMany(o => o.Items
                        .Where(i => i.ProductId == product.Id &&
                                    i.Quantity > 0)
                        .Select(i => new
                        {
                            OrderId = o.Id,
                            Item = i
                        }))
                    .GroupBy(x => x.OrderId);

                bool hasSales = false;

                foreach (var group in sales)
                {
                    hasSales = true;

                    if (!orderReportMap.TryGetValue(
                        group.Key, out int orderReportId))
                    {
                        return BadRequest(
                            $"Order report for order {group.Key} not found. Generate order reports first.");
                    }

                    if (product.Cost == null)
                    {
                        return BadRequest(
                            $"Product {product.Id} has no import cost");
                    }

                    int quantity = group.Sum(x => x.Item.Quantity);

                    decimal revenue = group.Sum(x =>
                        x.Item.TotalPrice != 0
                            ? x.Item.TotalPrice
                            : x.Item.UnitPrice * x.Item.Quantity);

                    decimal cost = product.Cost.Value * quantity;

                    newReports.Add(new ProductReport
                    {
                        OrderReportId = orderReportId,
                        ProductId = product.Id,
                        TotalSold = quantity,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = revenue - cost,
                        StockQuantity = product.Quantity
                    });
                }

                // Sản phẩm chưa bán được
                if (!hasSales)
                {
                    newReports.Add(new ProductReport
                    {
                        OrderReportId = null,
                        ProductId = product.Id,
                        TotalSold = 0,
                        Revenue = 0,
                        Cost = 0,
                        Profit = 0,
                        StockQuantity = product.Quantity
                    });
                }
            }

            _db.ProductReports.AddRange(newReports);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Product reports generated successfully",
                totalProducts = products.Count,
                totalReports = newReports.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, ex.Message);
        }
    }

    // DELETE /reports/products/{id}
    // id ở đây là product_id
    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProductReport(int id)
    {
        var reports = await _db.ProductReports
            .Where(x => x.ProductId == id)
            .ToListAsync();

        if (!reports.Any())
            return NotFound("Product report not found");

        _db.ProductReports.RemoveRange(reports);

        await _db.SaveChangesAsync();

        return Ok("Product reports deleted successfully");
    }
}