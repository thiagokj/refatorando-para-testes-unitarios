using Store.Domain.Entities;
using Store.Domain.Repositories.Interfaces;

namespace Store.Tests.Repositories
{
    public class FakeProductRepository : IProductRepository
    {
        public IEnumerable<Product> Get(IEnumerable<Guid> ids)
        {
            IList<Product> products = new List<Product>();
            products.Add(new Product("Produto 01", 10, true));
            products.Add(new Product("Produto 02", 20, true));
            products.Add(new Product("Produto 03", 30, true));
            products.Add(new Product("Produto 04", 40, false));
            products.Add(new Product("Produto 05", 50, false));

            return products;
        }
    }
}