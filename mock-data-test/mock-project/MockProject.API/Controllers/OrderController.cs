using Microsoft.AspNetCore.Mvc;
using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;

namespace MockProject.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// API thực thi toàn bộ luồng quy trình: Khách gọi món -> Bếp kiểm tra nguyên liệu -> Xác nhận nhận đơn -> Thanh toán -> Hoàn tất.
    /// </summary>
    /// <param name="request">Thông tin khách hàng, danh sách món chọn, và phương thức thanh toán</param>
    /// <returns>Kết quả xử lý đơn hàng theo quy trình</returns>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessOrderAsync([FromBody] ProcessOrderRequest request)
    {
        if (request == null || request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { Message = "Danh sách món ăn không được để trống." });
        }

        var response = await _orderService.ProcessOrderAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Tra cứu thông tin đơn hàng theo Id
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderByIdAsync(Guid id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound(new { Message = $"Không tìm thấy đơn hàng với Id: {id}" });
        }
        return Ok(order);
    }

    /// <summary>
    /// Tra cứu thực đơn và tồn kho nguyên liệu bếp hiện có (hỗ trợ kiểm thử happy path / exception path)
    /// </summary>
    [HttpGet("menu")]
    public async Task<IActionResult> GetMenuAsync([FromServices] IKitchenService kitchenService)
    {
        var inventory = await kitchenService.GetMenuAndInventoryAsync();
        return Ok(inventory);
    }
}

