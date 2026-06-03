namespace ScanNow.Application.Features.Kitchen.DTOs
{
    public class ConfirmKitchenItemsRequest
    {
        public List<Guid> OrderItemIds { get; set; } = new();
    }

    public class ConfirmKitchenItemsResponse
    {
        public int ItemsConfirmed { get; set; }
        public List<Guid> AffectedOrderIds { get; set; } = new();
    }
}
