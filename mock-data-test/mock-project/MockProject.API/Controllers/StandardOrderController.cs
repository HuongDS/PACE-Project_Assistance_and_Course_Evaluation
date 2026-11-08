using Microsoft.AspNetCore.Mvc;
using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;

namespace MockProject.API.Controllers;

/// <summary>
/// Nghiệp vụ 01: Quy Trình Gọi Món Và Thanh Toán Chuẩn (Happy / Standard Flow)
/// Thứ tự thực hiện: Khách chọn món -> Bếp kiểm tra nguyên liệu -> Xác nhận nhận đơn -> Khách thanh toán -> Hoàn tất.
/// </summary>
[ApiController]
[Route("api/standard-order")]
public class StandardOrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public StandardOrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Thực thi luồng chuẩn: Khách chọn món -> Bếp kiểm tra nguyên liệu -> Xác nhận -> Thanh toán -> Hoàn tất.
    /// </summary>
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
    /// Tra cứu thực đơn và tồn kho nguyên liệu bếp hiện có
    /// </summary>
    [HttpGet("menu")]
    public async Task<IActionResult> GetMenuAsync([FromServices] IKitchenService kitchenService)
    {
        var inventory = await kitchenService.GetMenuAndInventoryAsync();
        return Ok(inventory);
    }
}

