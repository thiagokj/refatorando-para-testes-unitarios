# Refatorando para UnitTests

Projeto para aprendizado e revisão de conceitos. [Curso balta.io](https://github.com/balta-io/7182)

## Práticas ruins em códigos

Alguns exemplos de códigos que funcionam, mas que não estão otimizados para performance, tratamento de exceções e testes de unidade.

1. Classes anêmicas - As Models possuem apenas propriedades primitivas e não tem comportamentos.
   Toda lógica de negócio está concentrada nos Controllers.

   ```csharp
   namespace Store.Models {
       public class Customer {
           public int Id { get; set; } // Poderia ser do tipo GUID, contemplando validação
           public string Name { get; set; } // Poderia ser do tipo Name, contemplando validação
           public string Email { get; set; } // Poderia ser do tipo Email, contemplando validação
       }
   }

   namespace Store.Models {
       public class PromoCode {
           public int Id { get; set; } // Poderia ser um GUID.
           public string Code { get; set; } // poderia ser um tipo Code
           public decimal Value { get; set; }
           public DateTime ExpireDate { get; set; }

           // Poderíamos ter métodos aqui para geração e validação do código promocional
       }
   }
   ```

1. Alto acoplamento - Conforme citado, toda lógica está concentrada em um Controller, sendo necessário refatorar.
   Problemas no único método do controller chamado **Place**.
   Varias conexões sendo instanciadas no controller. Deveria ser instanciada uma conexão fora do Controller, possibilitando criar casos de testes sem depender do banco de dados.

   ```csharp
    public class OrderController : Controller
       {
           [Route("v1/orders")]
           [HttpPost]
           public async Task<string> Place(string customerId, string zipCode, ...)
           {
               // #1 - Recupera o cliente
               Customer customer = null;
               // Vários trechos com a criação de conexão
               using (var conn = new SqlConnection("CONN_STRING"))
               {
                   customer = conn.Query<Customer>
                       // Usar o astérico retorna todos os campos da tabela, gastando mais recursos.
                       ("SELECT * FROM CUSTOMER WHERE ID=" + customerId)
                       .FirstOrDefault();
               }
          ...
           }
       }
   ```

1. Não há tratamento de exceções no calculo do frete.

   ```csharp
   ...
   // Continuação do Place

   // #2 - Calcula o frete
   decimal deliveryFee = 0;

   // Deveria tratar com try/catch, retry pattern...
   var request = new HttpRequestMessage(HttpMethod.Get, "URL/" + zipCode);
   request.Headers.Add("Accept", "application/json");
   request.Headers.Add("User-Agent", "HttpClientFactory-Sample");

   // Gera outra dependência, agora com o HttpClient, tornando muito difícil um Mock para testes.
    using (HttpClient client = new HttpClient())
   {
       var response = await client.SendAsync(request);
       if (response.IsSuccessStatusCode)
       {
           deliveryFee = await response.Content.ReadAsAsync<decimal>();
       }
       else
       {
           // # Caso não consiga obter a taxa de entrega o valor padrão é 5
           deliveryFee = 5;
       }
   }
   ```

1. Necessário reidratar (ler novamente) os produtos do banco de dados para calcular o subtotal.

   ```csharp
   ...
   // Continuação do Place

   // #3 - Calcula o total dos produtos
   decimal subTotal = 0;
   for (int p = 0; p < products.Length; p++)
   {
       var product = new Product();
       // Novamente outro acesso ao banco. Poderia ser feito tudo em uma única conexão.
       using (var conn = new SqlConnection("CONN_STRING"))
       {
           product = conn.Query<Product>
               // Novamente retornando todos os campos da tabela, deixando a query lenta.
               ("SELECT * FROM PRODUCT WHERE ID=" + products[p])
               .FirstOrDefault();
       }
       subTotal += product.Price;
   }
   ```

1. Não há como testar se o código promocional expirou.

   ```csharp
   ...
   // Continuação do Place

   // #4 - Aplica o cupom de desconto
   decimal discount = 0;

   // Outra conexão com banco de dados
   using (var conn = new SqlConnection("CONN_STRING"))
   {
       // E se o cupom vier nulo ou sem nenhuma informação? Não há tratamento.
       var promo = conn.Query<PromoCode>
           // Novamente o asterisco retornando todos os campos da tabela, deixando a consulta lenta.
           ("SELECT * FROM PROMO_CODES WHERE CODE=" + promoCode)
           .FirstOrDefault();

       // Não há como testar o promo code, pois não é possível gerar um caso de testes.
       if (promo.ExpireDate > DateTime.Now)
       {
           // E se o valor promo.Value não estiver preenchido? Não há tratamento.
           discount = promo.Value;
       }
   }
   ```

1. Aqui os dados são inferidos diretamente no pedido. Isso pode gerar problemas em produção, caso alguém faça uma atribuição indevida de valor para testes.

   ```csharp
   ...
   // Continuação do Place

   // #5 - Gera o pedido
   // O pedido é instanciado aqui e não podemos mitigar possíveis erros.
   var order = new Order();
   order.Code = Guid.NewGuid().ToString().ToUpper().Substring(0, 8);
   order.Date = DateTime.Now;
   order.DeliveryFee = deliveryFee;
   order.Discount = discount;
   order.Products = products;
   order.SubTotal = subTotal;

   // #6 - Calcula o total
   order.Total = subTotal - discount + deliveryFee;
   //order.Total = 999. Alguém poderia deixar esse código aqui e alterar os novos pedidos na produção.

   // #7 - Retorna
   return $"Pedido {order.Code} gerado com sucesso!";
   ```

Agora vamos organizar os passos para refatorar e melhorar o código.

## Modelagem de Domínio

1. Crie uma pasta para iniciar um novo projeto. Em seguida crie uma subpasta com o nome do **Projeto.Domain**.

   As regras de negócio devem ficar no domínio, que é o core da nossa aplicação.

   Para iniciar, criamos as entidades, fazemos testes e garantimos que todo o fluxo de processo esteja ok. Posteriormente podem ser adicionados repositórios, queries, serviços, etc.

   O Domain Driven Design é sobre isso, executar testes na aplicação. O domínio deve ser o mais puro possível, com o mínimo de pacotes e firulas.

1. Crie as Entidades com a representação básica dos dados. Posteriormente será feito aprimoramento.

   ```csharp
   public class Customer
   {
       public Customer(string name, string email)
       {
           Name = name;
           Email = email;
       }

       public string Name { get; private set; }
       public string Email { get; private set; }
   }
   ```

1. Crie uma Entidade Base (Entity.cs). Essa entidade é usada para compartilhar as propriedades similares entre todas as Entidades via herança.

   ```csharp
   public abstract class Entity
   {
       public Entity()
       {
           Id = Guid.NewGuid();
           // Caso o Id seja lido no banco de dados
           // utilizando ORMs como Dapper e EF, temos um proxy e não há problemas na atribuição.
       }

       public Guid Id { get; private set; }
   }
   ```

1. Toda vez que temos uma condicional no código (if, switch case, etc), temos possíveis caminhos diferentes para execução. Essa quantidade de caminhos pode gerar mais casos de testes, despendendo mais tempo do desenvolvedor para criação desses testes.

   Há uma métrica chamada de **complexidade ciclomática** que quantifica a complexidade de um código com base na quantidade de caminhos independentes que podem ser percorridos durante a sua execução. Essa métrica é amplamente utilizada em engenharia de software para avaliar a qualidade do código e identificar possíveis problemas de manutenção.

   Portanto, é importante manter a complexidade ciclomática em níveis razoáveis, para facilitar a compreensão e manutenção do código. Isso pode ser alcançado por meio de técnicas como a refatoração de código, que busca simplificar as estruturas condicionais e reduzir a quantidade de caminhos possíveis.

   ```csharp
       public class Discount : Entity
     {
         public Discount(decimal amount, DateTime expireDate)
         {
             Amount = amount;
             ExpireDate = expireDate;
         }

         public decimal Amount { get; private set; }
         public DateTime ExpireDate { get; private set; }

         public bool IsValid()
         {
             // Se o 1º valor for menor que o 2º, retorna menor que zero.
             return DateTime.Compare(DateTime.Now, ExpireDate) < 0;
         }

         public decimal Value()
         {
            // Se o cupom por válido, retorna o valor do cupom. Se não, retorna zero.
             return IsValid() ? Amount : 0;
         }
     }
   ```

   Temos que facilitar o uso externo do domínio. Tudo que estiver fora do domínio, deve ser organizado dentro do domínio e depois ser disponibilizado. No exemplo, temos os métodos que já fazem a validação do cupom de desconto,
   tornando o caminho único para esse tipo de validação e teste.

1. Pense na blindagem de código, onde os valores são passado por referência (ex: o preço do produto pertence ao produto e só é passado via construtor na entidade Produto).

   ```csharp
   public class Product : Entity
   {
       public Product(string title, decimal price, bool active)
       {
           Title = title;
           Price = price;
           Active = active;
       }

       public string Title { get; private set; }
       public decimal Price { get; private set; }
       public bool Active { get; private set; }

       // Poderia haver um ChangePrice() para atualizar o preço aqui.
   }

    public class OrderItem : Entity
    {
        // Veja que não passamos todas as propriedades no construtor, forçando a blindagem do código.
        public OrderItem(Product product, int quantity)
        {
            // Como recebemos a instancia do Produto,
            // podemos acessar o preço e fazer um tratamento caso ele não exista.
            Product = product;
            Price = Product != null ? product.Price : 0;
            Quantity = quantity;
        }

        public Product Product { get; private set; }
        public decimal Price { get; private set; }
        public int Quantity { get; private set; }

        // Aqui começamos a delegar as responsabilidades nas entidades,
        // segmentando o código e facilitando os casos de testes.
        public decimal Total()
        {
            return Price * Quantity;
        }
    }
   ```

1. Para finalizar temos a Entidade Pedido, que contempla todos os itens do pedido

   ```csharp
   public class Order : Entity
   {
        // Novamente blindamos o construtor,
        // permitindo apenas parâmetros respectivos a essa entidade
        public Order(Customer customer, decimal deliveryFee, Discount discount)
        {
           Customer = customer;
           Date = DateTime.Now;
           Number = Guid.NewGuid().ToString().Substring(0, 8);
           Status = EOrderStatus.WaitingPayment;
           DeliveryFee = deliveryFee; // A taxa de entrega pode vir de um serviço externo.
           Discount = discount; // As regras de desconto são aplicadas apenas na entidade Desconto.
           Items = new List<OrderItem>();
        }

        public Customer Customer { get; private set; }
        public DateTime Date { get; private set; }
        public string Number { get; private set; }
        public EOrderStatus Status { get; private set; }
        public decimal DeliveryFee { get; private set; }
        public Discount Discount { get; private set; }
        public IList<OrderItem> Items { get; private set; }

        public void AddItem(Product product, int quantity)
        {
            var item = new OrderItem(product, quantity);
            Items.Add(item);
        }

        public decimal Total()
        {
           decimal total = 0;
           foreach (var item in Items)
           {
               // A regra de totalização do Item está blindada nas regras de negócio da entidade Item.
               // Caso haja mudança na regra, é necessário alterar apenas em um único lugar.
               total += item.Total();
           }

           total += DeliveryFee;

           // Se houver desconto, as regras de negócio são aplicadas na entidade Desconto, não havendo
           // necessidade de verificações adicionais, pois não é responsabilidade da entidade Pedido.
           total -= Discount != null ? Discount.Value() : 0;

           return total;
        }

        public void Pay(decimal amount)
        {
           if (amount == Total())
           {
               this.Status = EOrderStatus.WaitingDelivery;
           }
        }

        public void Cancel()
        {
           Status = EOrderStatus.Canceled;
        }
    }
   ```

## Validações

Após realizar a modelagem inicial e refatorar, é necessário aplicar as validações.

```csharp
...
var item = new OrderItem(product, quantity);
Items.Add(item);

// Esse código vai aparecer em muitos lugares no código, gerando diversos casos de testes.
if (product == null) return;
if (quantity < 0) return;
...
```

Note que essas condicionais tendem a se repetir em todas as partes do projeto. Para evitar isso, há uma abordagem melhor de se trabalhar chamada de **Design by Context**. O Design por contexto consiste em encapsular as condicionais, torna-las em métodos, testar e reutilizar esse código.

Obs: Evite lançar **exceptions**. As exceções devem ser usadas para situações não previstas como:

- Falha de conexão
- Serviços indisponíveis
- Bloqueio por regra de segurança de rede

Podemos então trabalhar com **Notificações de Domínio**. A sugestão é usar o Flunt - Fluent Validations. Esse pacote já contempla as validações comuns e torna o código mais fácil de validar e testar.
Adicione o pacote **dotnet add package Flunt**.

Refatorando as entidades com notificações temos:

```csharp
...
public void AddItem(Product product, int quantity)
{
    // Só adiciona um item no pedido caso o item seja valido
    var item = new OrderItem(product, quantity);
    if (item.IsValid) // item.Notifications | Temos acesso as notificações caso o item seja invalido.
        Items.Add(item);
}

// A validação acontece na entidade OrderItem (Item do pedido), no método construtor
...
public OrderItem(Product product, int quantity)
{
    // O item do pedido não pode ter um produto inválido e a quantidade tem que ser maior que 0.
    AddNotifications(new Contract<OrderItem>()
        .Requires()
        .IsNotNull(product, "OrderItem.Product", "O produto não pode ser nulo")
        .IsGreaterThan(quantity,
         0, "OrderItem.quantity", "A quantidade deve ser maior que zero")
    );

    Product = product;
    ...
}

// Trecho de validação ao criar um objeto Pedido via construtor
...
 public Order(Customer customer, decimal deliveryFee, Discount discount)
{
    // É criado um contrato do tipo Order(Pedido) que requer que o customer(Cliente) não seja nulo.
    AddNotifications(new Contract<Order>()
        .Requires()
        .IsNotNull(customer, "Customer", "Cliente inválido")
    );

    Customer = customer;
    Date = DateTime.Now;
    ...
}
```

## Testando as Entidades

Organize os arquivos criando o Store.Teste. Crie um projeto de testes dentro da pasta com **dotnet new mstest**.

Adicione a referencia dos testes ao arquivo csproj com **dotnet add reference ..\Store.Domain\Store.Domain.csproj**

Agora crie a entidade com a estrutura para testes:

```csharp
// Decoração para indicar que é uma classe de testes, habilitando funções de debug.
[TestClass]
public class OrderTests
{
    // Decoração indicando que é um método de testes, habilitando funções de debug.
    [TestMethod]
    // Decoração agrupando os testes por categoria. Funções visíveis no Visual Studio.
    [TestCategory("Domain")]
    // Deve retornar um novo pedido com um numero de 8 caracteres
    public void ShouldReturnNewOrderWithNumberHavingEightCharacters()
    {
        Assert.Fail();
    }
}
```

Há uma metodologia padrão de mercado que segue o fluxo de testes chamada de **Red, Green, Refactor**.

Esse padrão consiste em:

🟥 Red -> Criar todos os testes, adicionar a instrução Assert.Fail() e executar um dotnet test, para que todos sejam reprovados.

🟩 Green -> Alterar os testes com instruções simples de comportamento, testar individualmente validando cada aprovação.

🖥️ Refactor -> Reescrever os testes prevendo os mais variados tipos de situações que podem ocorrer na aplicação.

```csharp
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
    public void ShouldReturnSuccessWhenTotalOrderAre60WithDeliveryFeeOf10()
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
        Assert.AreEqual(true, order.IsValid);
    }
}
```

## Testando Queries

Para testar as queries, é necessário converter os SQL's em Expressions, abstraindo as consultas da dependência do banco de dados.

```csharp
public static class ProductQueries
    {
        public static Expression<Func<Product, bool>> GetActiveProducts()
        {
            return x => x.Active;
        }

        // Usando expression body quando há apenas um retorno, reduzindo linhas do código.
        public static Expression<Func<Product, bool>> GetInactiveProducts()
            => x => x.Active == false;
    }
```

## Repositórios

Os repositórios são uma abstração do banco, criando uma unidade de acesso único aos dados.

Ex: ClienteRepo.cs -> fica responsável pelo CRUD do Clientes e também quaisquer outros métodos que possam ser úteis para tornar mais fácil trabalhar com os dados.

Facilita muito a migração para outros bancos, para micro serviços, leitura de um arquivo texto e qualquer fonte de dados.

Usando os repositórios temos o desacoplamento, dependo apenas da interface e não da implementação. Dessa forma não há um vinculo com um framework específico.

```csharp
namespace Store.Domain.Repositories.Interfaces
{
    public interface ICustomerRepository
    {
        // Retorna o cliente passando o numero do CPF ou CNPJ.
        Customer Get(string document);
    }

    public interface IProductRepository
    {
        // Retorna uma lista de Produtos com base no ID informado.
        IEnumerable<Product> Get(IEnumerable<Guid> ids);
    }

    public interface IDeliveryFeeRepository
    {
        // Retorna o valor do frete com base no CEP informado.
        decimal Get(string zipCode);
    }
}
```

## Mocks com base nos Repositórios

É hora de simular os dados para efetuar testes. Esse é mais uma vantagem de usar o padrão de repositórios, a facilidade para fazer testes.

```csharp
public class FakeCostumerRepository : ICustomerRepository
{
    // Implementação fácil da interface para retornar dados fake
    public Customer Get(string document)
    {
        if (document == "12345678911")
            return new Customer("Bruce Wayne", "batman@dc.mock");

        return null;
    }
}
```

O Mock deve fazer sentido para os testes posteriores, crie cases mais próximos dos reais!

```csharp
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

// Testando somente as queries
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
        // Retorna apenas os 3 produtos ativos
        public void ShouldReturn3ActiveProducts()
        {
            var result = _products
                .AsQueryable()
                .Where(ProductQueries.GetActiveProducts());

            Assert.AreEqual(3, result.Count());
        }
    }
```

## Comandos

Vamos para parte da escrita. O CQRS é a separação de leitura e escrita.

Os Commands são as ações para criar os objetos na aplicação, com todas as informações necessárias.

Vamos pensar no Command como um objeto de transporte. São a entrada das informações para o domínio.

```csharp
// Comando para fazer o Fail Fast Validation, melhorando a efetividade do código.
// Retornar antecipadamente erros é uma forma de evitar gastar recursos desnecessários.
public interface ICommand
{
    bool Validate();
}
```

Crie também um Comando de retorno. Dessa forma padronizamos os tipos retornados, facilitando o tratamento e debug no Frontend da aplicação.

```csharp
public class GenericCommandResult : ICommandResult
{
    // Retorno padrão, sempre trazendo a resposta da requisição se foi ok ou não,
    // a mensagem da resposta e os dados.
    public GenericCommandResult(bool success, string message, object data)
    {
        Success = success;
        Message = message;
        Data = data;
    }

    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
    // Poderíamos ter uma propriedade Erros aqui, retornando todos os erros em uma lista
}
```

Agora é possível criar um comando para criar um item do pedido, passando pra ele as validações requeridas.

```csharp
// Implementado o Notifiable do pacote Flunt, trazendo as validações comuns de forma fácil.
// Implementado o ICommand, que exige a implementação do método Validate.
public class CreateOrderItemCommand : Notifiable<Notification>, ICommand
    {
        public CreateOrderItemCommand() { }

        public CreateOrderItemCommand(Guid product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }

        public Guid Product { get; set; }
        public int Quantity { get; set; }

        // O item do pedido deve possuir um código de produto de 32 caracteres.
        // A quantidade do item do pedido deve ser maior que zero.
        public bool Validate()
        {
            AddNotifications(new Contract<CreateOrderItemCommand>()
                .Requires()
                .IsLowerOrEqualsThan(Product.ToString(), 32,
                "Product", "Produto inválido")
                .IsGreaterThan(Quantity, 0, "Quantity", "Quantidade inválida")
            );

            return IsValid;
        }
    }
```

Agora vamos executar o teste, o teste contempla a **Validação rápida de falha**, ou seja, detecta um erro de forma antecipada, evitando que o comando faça processamentos, acesse banco de dados, use apis, etc... para somente no final do processo gerar um erro. Essa economia no fluxo de validação é extremamente importante!!!

```csharp
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

        // O validade adiciona notificações de falha caso as condições especificadas
        // não sejam atendidas.
        command.Validate();

        // Verifica o retorno do command.Validate(), informando o comando é valido ou não.
        Assert.AreEqual(false, command.IsValid);
    }
}
```

## Handlers

Os **Handlers** tem a função de gerenciar o fluxo da aplicação. Normalmente as aplicações tem os Inputs do usuário (entradas), processamento e Output (retorno / saída / resultado).

Handler -> Recebe um comando -> Gerencia o fluxo de pedido -> Retorna um comando para tela (CommandResult).

Nesse caso, temos o fluxo de processo para gerar um pedido:

1. Cadastro de cliente.
2. Cadastro de produto.
3. Cadastro de cupom de desconto.
4. Cadastro de item do pedido.
5. Cadastro de pedido com todos os itens anteriores.

Utilizando um manipulador, tornamos ainda mais fácil realizar testes no fluxo da aplicação, facilitando a investigação em caso de problemas.

```csharp
// IHandler de TIPO genérico (T) com implementação obrigatória de ICommand
// Esse T significa o TIPO. É uma convenção que indica o uso genérico de qualquer classe.
public interface IHandler<T> where T : ICommand
{
    // Ao implementar a interface, será necessário implementar o retorno do comando
    // passando o tipo da classe do comando
    ICommandResult Handle(T command);
}
```

Seguindo a organização do projeto, teremos um **OrderHandler**. O OrderHandler irá herdar e implementar:

```csharp
// Herda de Notifiable do tipo Notificação (pacote Flunt), controlando as validações.
// Implementa o IHandler do tipo de comando CreateOrderCommand, retornando o resultado
// do comando de forma padronizada.
public class OrderHandler : Notifiable<Notification>, IHandler<CreateOrderCommand>
...
```

A sequencia lógica de declarações no OrderHandler:

Serviços externos -> Repositórios -> Construtor com repositórios -> Implementação do resultado do comando.

```csharp
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
        // Sempre começando com Fail Fast Validation
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
```

## Testando o Handler

E para finalizar alguns testes validando o fluxo de execução.

```csharp
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
```
