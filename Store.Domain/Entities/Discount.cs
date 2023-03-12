using Flunt.Validations;

namespace Store.Domain.Entities
{
    public class Discount : Entity
    {
        public Discount(decimal amount, DateTime expireDate)
        {
            AddNotifications(new Contract<Discount>()
                .Requires()
                .IsGreaterThan(expireDate, DateTime.Now,
                 "Discount.ExpireDate", "Desconto expirado")
            );

            Amount = amount;
            ExpireDate = expireDate;
        }

        public decimal Amount { get; private set; }
        public DateTime ExpireDate { get; private set; }

        public new bool IsValid()
        {
            // Se o 1º valor for menor que o 2º, retorna menor que zero.
            return DateTime.Compare(DateTime.Now, ExpireDate) < 0;
        }

        public decimal Value()
        {
            // Se o cupom for válido, retorna o valor do cupom. Se não, retorna zero.
            return IsValid() ? Amount : 0;
        }
    }
}