using Store.Domain.Entities;
using Store.Domain.Queries;

namespace Store.Tests.Queries
{
    [TestClass]
    public class ProductQueriesTests
    {
        private IList<Product> _products;

        public ProductQueriesTests()
        {
            _products = new List<Product>();
            _products.Add(new Product("Produto 01", 10, true));
            _products.Add(new Product("Produto 02", 20, true));
            _products.Add(new Product("Produto 03", 30, true));
            _products.Add(new Product("Produto 04", 40, false));
            _products.Add(new Product("Produto 05", 50, false));
        }

        [TestMethod]
        public void ShouldReturn3ActiveProducts()
        {
            var result = _products
                .AsQueryable()
                .Where(ProductQueries.GetActiveProducts());

            Assert.AreEqual(3, result.Count());

        }

        [TestMethod]
        public void ShouldReturn2InactiveProducts()
        {
            var result = _products
                .AsQueryable()
                .Where(ProductQueries.GetInactiveProducts());

            Assert.AreEqual(2, result.Count());

        }
    }
}