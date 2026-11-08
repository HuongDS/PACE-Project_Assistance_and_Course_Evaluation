using MockProject.Business.DTOs;
using MockProject.Business.Interfaces;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Business.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        var payment = new PaymentRecord
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaidAt = DateTime.UtcNow,
            IsSuccessful = true,
            TransactionCode = $"TXN-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
        };

        await _paymentRepository.SavePaymentAsync(payment);

        return new PaymentResponse
        {
            IsSuccess = true,
            TransactionId = payment.TransactionCode,
            Message = $"Thanh toán thành công số tiền {request.Amount:N0} VNĐ qua phương thức '{request.PaymentMethod}'."
        };
    }
}

