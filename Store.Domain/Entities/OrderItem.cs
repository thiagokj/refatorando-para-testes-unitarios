using Flunt.Validations;

namespace Store.Domain.Entities
{
    public class OrderItem : Entity
    {
        public OrderItem(Product product, int quantity)
        {
            AddNotifications(new Contract<OrderItem>()
                .Requires()
                .IsNotNull(product, "OrderItem.Product", "O produto não pode ser nulo")
                .IsGreaterThan(quantity,
                 0, "OrderItem.quantity", "A quantidade deve ser maior que zero")
            );

            Product = product;
            Price = Product != null ? product.Price : 0;
            Quantity = quantity;
        }

        public Product Product { get; private set; }
        public decimal Price { get; private set; }
        public int Quantity { get; private set; }

        public decimal Total()
        {
            return Price * Quantity;
        }
    }
}