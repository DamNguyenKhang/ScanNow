using ScanNow.Application.Features.Order.DTOs;
using ScanNow.Domain.Entities;

namespace ScanNow.Application.Mappers
{
    public static class CustomerOrderMapper
    {
        public static CustomerOrderResponse Map(Order order)
        {
            return new CustomerOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                BranchId = order.BranchId,
                TableId = order.TableId,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerNote = order.CustomerNote,
                SubTotal = order.SubTotal,
                VatPercent = order.VatPercent,
                VatAmount = order.VatAmount,
                ServiceChargePercent = order.ServiceChargePercent,
                ServiceChargeAmount = order.ServiceChargeAmount,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                OrderSource = order.OrderSource,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Items = order.Items.Select(MapItem).ToList()
            };
        }

        private static CustomerOrderItemResponse MapItem(OrderItem item)
        {
            return new CustomerOrderItemResponse
            {
                OrderItemId = item.Id,
                MenuItemId = item.MenuItemId,
                MenuItemName = item.MenuItemName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                SubTotal = item.SubTotal,
                Note = item.Note,
                Status = item.Status,
                EstimatedCookingMinutes = item.EstimatedCookingMinutes
            };
        }
    }
}
