using System.Collections.Concurrent;
using MockProject.Data.Entities;
using MockProject.Data.Interfaces;

namespace MockProject.Data.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private static readonly ConcurrentDictionary<Guid, PaymentRecord> Payments = new();

    public Task<PaymentRecord> SavePaymentAsync(PaymentRecord payment)
    {
        Payments[payment.OrderId] = payment;
        return Task.FromResult(payment);
    }

    public Task<PaymentRecord?> GetPaymentByOrderIdAsync(Guid orderId)
    {
        Payments.TryGetValue(orderId, out var payment);
        return Task.FromResult(payment);
    }
}

