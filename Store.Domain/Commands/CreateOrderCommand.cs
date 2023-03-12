using Flunt.Notifications;
using Flunt.Validations;
using Store.Domain.Commands.Interfaces;

namespace Store.Domain.Commands;

public class CreateOrderCommand : Notifiable<Notification>, ICommand
{
    public CreateOrderCommand()
    {
        Items = new List<CreateOrderItemCommand>();
    }

    public CreateOrderCommand(
        string customer,
        string zipCode,
        string promoCode,
        IList<CreateOrderItemCommand> items
    )
    {
        Customer = customer;
        ZipCode = zipCode;
        PromoCode = promoCode;
        Items = items;
    }

    public string Customer { get; set; }
    public string ZipCode { get; set; }
    public string PromoCode { get; set; }
    public IList<CreateOrderItemCommand> Items { get; set; }

    public void Validate()
    {
        AddNotifications(new Contract<CreateOrderCommand>()
            .Requires()
            .AreEquals(Customer, 11, "CreateOrderCommand.Customer",
             "Cliente inválido")
            .AreEquals(ZipCode, 8, "CreateOrderCommand.ZipCode",
             "CEP inválido")
            .IsNotNullOrEmpty(ZipCode, "CreateOrderCommand.ZipCode")
        );
    }
}