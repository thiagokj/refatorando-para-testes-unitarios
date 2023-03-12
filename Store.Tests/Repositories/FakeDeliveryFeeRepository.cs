using Store.Domain.Repositories.Interfaces;

namespace Store.Tests.Repositories
{
    public class FakeDeliveryFeeRepository : IDeliveryFeeRepository
    {
        public decimal Get(string zipCode)
        {
            if (zipCode == "12345678")
                return 20;

            return 10;
        }
    }
}