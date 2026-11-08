using MockProject.Data.Entities;

namespace MockProject.Data.Interfaces;

public interface IPaymentRepository
{
    Task<PaymentRecord> SavePaymentAsync(PaymentRecord payment);
    Task<PaymentRecord?> GetPaymentByOrderIdAsync(Guid orderId);
}

