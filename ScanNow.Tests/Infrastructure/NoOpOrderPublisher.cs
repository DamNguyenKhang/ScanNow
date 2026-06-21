using ScanNow.Application.Abstractions;
using ScanNow.Application.Features.Order.DTOs;

namespace ScanNow.Tests.Infrastructure;

/// <summary>Discards SignalR publish calls so tests don't need a real hub.</summary>
public sealed class NoOpOrderPublisher : IOrderUpdatePublisher
{
    public Task PublishOrderUpdatedAsync(CustomerOrderResponse order, CancellationToken ct = default)
        => Task.CompletedTask;
}
