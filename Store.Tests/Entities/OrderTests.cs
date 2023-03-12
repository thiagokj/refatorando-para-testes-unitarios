using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Tests.Entities;

[TestClass]
public class OrderTests
{
    private readonly Customer _customer = new Customer("Cliente", "cliente@emaildocliente.sufix");
    private readonly Discount _discount = new Discount(10, DateTime.Now.AddMonths(1));
    private readonly Discount _expiredDiscount = new Discount(20, DateTime.Now.AddMonths(-1));
    private readonly Product _product = new Product("Produto 1", 10, true);
    private readonly Product _product2 = new Product("Produto 2", 30, true);


    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenANewOrderWithNumberContainsEightCharacters()
    {
        var order = new Order(_customer, 0, null);
        Assert.AreEqual(8, order.Number.Length);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenOrderStatusIsWaitingPayment()
    {
        var order = new Order(_customer, 0, null);
        Assert.AreEqual(EOrderStatus.WaitingPayment, order.Status);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenOrderStatusAfterPaymentIsWaitingDelivery()
    {
        var orderItem = new OrderItem(_product, 1);
        var order = new Order(_customer, 0, null);

        order.AddItem(orderItem.Product, orderItem.Quantity);
        order.Pay(10);

        Assert.AreEqual(EOrderStatus.WaitingDelivery, order.Status);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenOrderStatusIsCanceledAfterCancelOrder()
    {
        var order = new Order(_customer, 0, null);
        order.Cancel();

        Assert.AreEqual(EOrderStatus.Canceled, order.Status);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnErrorWhenOrderItemNotHaveAProduct()
    {
        var orderItem = new OrderItem(null, 1);

        Assert.AreEqual(null, orderItem.Product);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnErrorWhenOrderItemInOrderNotHaveAProduct()
    {
        var order = new Order(_customer, 0, null);
        order.AddItem(null, 1);

        Assert.AreEqual(0, order.Items.Count);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnErrorWhenQuantityOfOrderItemIsLowerOrEqualsThanZero()
    {
        var order = new Order(_customer, 0, null);
        order.AddItem(_product, 0);
        order.AddItem(_product, -1);

        Assert.AreEqual(0, order.Items.Count);
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenOrderTotalAre50()
    {
        var order = new Order(_customer, 0, null);
        order.AddItem(_product, 2);
        order.AddItem(_product2, 1);

        Assert.AreEqual(50, order.Total());
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenTotalOrderAre60BecauseExpiredDiscount()
    {
        var order = new Order(_customer, 10, _expiredDiscount);
        order.AddItem(_product, 5);

        Assert.AreEqual(60, order.Total());
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnErrorWhenOrderTotalAre60BecauseInvalidDiscount()
    {
        var order = new Order(_customer, 10, null);
        order.AddItem(_product, 5);

        Assert.AreEqual(60, order.Total());
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenTotalOrderAre50WithValidDiscount()
    {
        var order = new Order(_customer, 10, _discount);
        order.AddItem(_product, 5);

        Assert.AreEqual(50, order.Total());
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnSuccessWhenTotalOrderAre60WithFeeTaxOf10()
    {
        var order = new Order(_customer, 10, _discount);
        order.AddItem(_product, 6);

        Assert.AreEqual(60, order.Total());
    }

    [TestMethod]
    [TestCategory("Domain")]
    public void ShouldReturnErrorWhenOrderNotHaveACustomer()
    {
        var order = new Order(null, 10, _discount);
        Assert.AreEqual(false, order.IsValid);
    }
}