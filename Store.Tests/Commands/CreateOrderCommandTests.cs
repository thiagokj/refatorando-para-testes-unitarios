using Store.Domain.Commands;

namespace Store.Tests.Commands
{
    [TestClass]
    public class CreateOrderCommandTests
    {
        [TestMethod]
        [TestCategory("Handlers")]
        public void ShouldReturnErrorWhenCommandIsInvalidAndNotGenerateOrder()
        {
            var command = new CreateOrderCommand();
            command.Customer = "";
            command.ZipCode = "11123456";
            command.PromoCode = "12345678";
            command.Items.Add(new CreateOrderItemCommand(Guid.NewGuid(), 1));
            command.Items.Add(new CreateOrderItemCommand(Guid.NewGuid(), 1));
            command.Validate();

            Assert.AreEqual(false, command.IsValid);
        }
    }
}