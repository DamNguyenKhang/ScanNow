namespace ScanNow.Application.Features.Cart.DTOs
{
    public class CartDto
    {
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
        public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);
    }
}
