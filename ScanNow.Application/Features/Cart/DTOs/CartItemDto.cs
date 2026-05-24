namespace ScanNow.Application.Features.Cart.DTOs
{
    public class CartItemDto
    {
        public Guid MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? SpecialRequest { get; set; }
        public string? ImageUrl { get; set; }
    }
}
