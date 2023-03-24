using Flunt.Notifications;
using Store.Domain.Commands;
using Store.Domain.Commands.Interfaces;
using Store.Domain.Entities;
using Store.Domain.Handlers.Interfaces;
using Store.Domain.Repositories.Interfaces;
using Store.Domain.Utils;

namespace Store.Domain.Handlers;

public class OrderHandler : Notifiable<Notification>, IHandler<CreateOrderCommand>
{
    // Sempre dependa da abstração e NUNCA da implementação.
    // Evite ficar acoplado a EF, Dapper ou qualquer outra forma de amarração de código usando Interfaces,
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeliveryFeeRepository _deliveryFeeRepository;
    private readonly IDiscountRepository _discountRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    // Geração de dependências para resolver posteriormente, seja na API, nos Mocks de Testes, etc.
    public OrderHandler(
        ICustomerRepository customerRepository,
        IDeliveryFeeRepository deliveryFeeRepository,
        IDiscountRepository discountRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _deliveryFeeRepository = deliveryFeeRepository;
        _discountRepository = discountRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public ICommandResult Handle(CreateOrderCommand command)
    {
        // Sempre começando com Fail Fast Validation ;)
        if (command.Validate())
            return new GenericCommandResult(false, "Pedido inválido", null);

        // 1. Recupera o cliente
        var customer = _customerRepository.Get(command.Customer);

        // 2. Calcula a taxa de entrega (frete)
        var deliveryFee = _deliveryFeeRepository.Get(command.ZipCode);

        // 3. Obtém o cupom de desconto
        var discount = _discountRepository.Get(command.PromoCode);

        // 4. Gera o pedido
        var products = _productRepository
            .Get(ExtractGuids.Extract(command.Items)).ToList();
        var order = new Order(customer, deliveryFee, discount);

        foreach (var item in command.Items)
        {
            var product = products
                .Where(x => x.Id == item.Product)
                .FirstOrDefault();
            order.AddItem(product, item.Quantity);
        }

        // 5. Agrupa as notificações em caso de erro
        AddNotifications(order.Notifications);

        // 6. Valida todo o processo com base nas notificações
        if (!IsValid)
            return new GenericCommandResult(false,
                 "Falha ao gerar o pedido", Notifications);

        // 7. Salva o pedido no banco de dados (ou qualquer outra fonte de dados)
        _orderRepository.Save(order);
        return new GenericCommandResult(true,
             $"Pedido {order.Number} gerado com sucesso", order);
    }
}