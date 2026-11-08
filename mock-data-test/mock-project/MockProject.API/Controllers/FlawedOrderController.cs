using Microsoft.AspNetCore.Mvc;
using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;

namespace MockProject.API.Controllers;

/// <summary>
/// Nghiệp vụ 02: Quy Trình Gọi Món Và Thanh Toán Sai (Nghịch Đảo Thứ Tự / Anti-pattern Flow)
/// Thứ tự lỗi: Thu tiền khách trước -> Mới tạo đơn gọi món -> Mới chuyển xuống bếp kiểm tra nguyên liệu.
/// </summary>
[ApiController]
[Route("api/flawed-order")]
public class FlawedOrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public FlawedOrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Thực thi luồng sai: Thu tiền khách trước -> Mới tạo đơn -> Bếp kiểm tra nguyên liệu.
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessOrderAsync([FromBody] ProcessOrderRequest request)
    {
        if (request == null || request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { Message = "Danh sách món ăn không được để trống." });
        }

        var response = await _orderService.ProcessFlawedOrderAsync(request);

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
}

