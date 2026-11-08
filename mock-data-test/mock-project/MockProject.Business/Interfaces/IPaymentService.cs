using MockProject.Business.DTOs;

namespace MockProject.Business.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
}

