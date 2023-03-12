using Store.Domain.Commands;
using Store.Domain.Handlers;
using Store.Domain.Repositories.Interfaces;
using Store.Tests.Repositories;

namespace Store.Tests.Handlers;

[TestClass]
public class OrderHandlerTests
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeliveryFeeRepository _deliveryFeeRepository;
    private readonly IDiscountRepository _discountRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public OrderHandlerTests()
    {
        _customerRepository = new FakeCostumerRepository();
        _deliveryFeeRepository = new FakeDeliveryFeeRepository();
        _discountRepository = new FakeDiscountRepository();
        _productRepository = new FakeProductRepository();
        _orderRepository = new FakeOrderRepository();
    }

    [TestMethod]
    [TestCategory("Handlers")]
    public void ShouldReturnErrorWhenCustomerIsInvalidNotGeneratingOrder()
    {
        var command = new CreateOrderCommand()
        {
            Customer = "",
            ZipCode = null,
            PromoCode = "12345678",
            Items = new List<CreateOrderItemCommand>()
                {
                    new CreateOrderItemCommand(Guid.NewGuid(), 1),
                    new CreateOrderItemCommand(Guid.NewGuid(), 1)
                }
        };

        var handler = new OrderHandler(
            _customerRepository,
            _deliveryFeeRepository,
            _discountRepository,
            _productRepository,
            _orderRepository
        );

        handler.Handle(command);
        Assert.AreEqual(false, command.IsValid);
    }

    [TestMethod]
    [TestCategory("Handlers")]
    public void ShouldReturnSuccessWhenCommandIsValidGeneratingOrder()
    {
        var command = new CreateOrderCommand()
        {
            Customer = "12345678911",
            ZipCode = "11123456",
            PromoCode = "12345678",
            Items = new List<CreateOrderItemCommand>()
                {
                    new CreateOrderItemCommand(Guid.NewGuid(), 1),
                    new CreateOrderItemCommand(Guid.NewGuid(), 1)
                }
        };

        var handler = new OrderHandler(
            _customerRepository,
            _deliveryFeeRepository,
            _discountRepository,
            _productRepository,
            _orderRepository
        );

        handler.Handle(command);
        Assert.AreEqual(true, handler.IsValid);
    }
}
