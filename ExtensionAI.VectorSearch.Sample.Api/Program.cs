using System.ComponentModel;
using System.Numerics;
using System.Text.Json;
using ExtensionAI.VectorSerach.Sample.Api;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

List<Product> products = new List<Product>();

products = new List<Product>
        {
new Product { Id = 1, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse with adjustable DPI.", Category = "Electronics", Price = 25.99m, Barcode = "WMOUSE-001" },
new Product { Id = 2, Name = "Gaming Keyboard", Description = "Mechanical keyboard with RGB lighting.", Category = "Electronics", Price = 79.99m, Barcode = "GKEY-002" },
new Product { Id = 3, Name = "Bluetooth Speaker", Description = "Portable speaker with high-quality sound.", Category = "Electronics", Price = 49.99m, Barcode = "BTSPEAK-003" },
new Product { Id = 4, Name = "Smartphone Stand", Description = "Adjustable stand for smartphones and tablets.", Category = "Accessories", Price = 15.99m, Barcode = "SPSTAND-004" },
new Product { Id = 5, Name = "Noise Cancelling Headphones", Description = "Over-ear headphones with active noise cancellation.", Category = "Electronics", Price = 199.99m, Barcode = "NCHEAD-005" },
new Product { Id = 6, Name = "USB-C Hub", Description = "Multi-port hub with HDMI and USB 3.0 support.", Category = "Accessories", Price = 34.99m, Barcode = "USBC-HUB-006" },
new Product { Id = 7, Name = "Fitness Tracker", Description = "Waterproof fitness tracker with heart rate monitor.", Category = "Wearables", Price = 59.99m, Barcode = "FITTRK-007" },
new Product { Id = 8, Name = "4K Monitor", Description = "27-inch 4K UHD monitor with HDR support.", Category = "Electronics", Price = 299.99m, Barcode = "4KMON-008" },
new Product { Id = 9, Name = "External SSD", Description = "1TB portable SSD with fast read/write speeds.", Category = "Storage", Price = 129.99m, Barcode = "EXTSSD-009" },
new Product { Id = 10, Name = "Wireless Charger", Description = "Fast wireless charger compatible with most devices.", Category = "Accessories", Price = 29.99m, Barcode = "WCHARG-010" },
new Product { Id = 11, Name = "Standard Keyboard", Description = "Standard keyboard with adjustable DPI.", Category = "Electronics", Price = 29.99m, Barcode = "SKEY-011" }
};

var ollamaUri = new Uri("http://localhost:11434/");

var ollamaChatClient = new OllamaChatClient(ollamaUri, "qwen3:14b");

IEmbeddingGenerator<string, Embedding<float>> _vectorGenerator = new OllamaEmbeddingGenerator(ollamaUri, "snowflake-arctic-embed2");

IChatClient _chatClient = default!;
_chatClient = new ChatClientBuilder(ollamaChatClient)
    .UseFunctionInvocation()
    .Build();

var _vectorStore = new InMemoryVectorStore();

IVectorStoreRecordCollection<int, Product> productsVector = _vectorStore.GetCollection<int, Product>("products");//vector search için collection oluşturma işlemi yapılıyor.
await productsVector.CreateCollectionIfNotExistsAsync();

foreach (var product in products)
{
    product.Vector = await _vectorGenerator.GenerateVectorAsync($"{product.Name} {product.Description}");
    await productsVector.UpsertAsync(product);
}

app.MapGet("/search", (string query) =>
{
      return GetResponse(query);
})
.WithName("Search");
async Task<string> GetResponse(string query)
{
    var chatOptions = new ChatOptions
    {

        ToolMode = ChatToolMode.Auto,
        Tools =
            [
                AIFunctionFactory.Create(VectorSearchAsync),
                AIFunctionFactory.Create(ProductSearchWithIdAsync)
            ]
    };

    var chatMessages = new List<ChatMessage>();

    chatMessages.Add(new ChatMessage(ChatRole.System, "Sen bir e‑ticaret asistanısın. ürünler ili ilgili bilgi almak istendiğinde methodları çağırabilirsin, method çağrısı yaptığında sana kullanıcının aramak istediği en yakın ürün veya ürünlerin bilgisi json formatında dönecek. bu bilgilerin içerisinde ürün ismi, açıklaması, kategorisi ve fiyatı olacak. bu sonucu yorumlayıp, kullanıcının istediği bilgiyi dönebilirsin."));

    chatMessages.Add(new ChatMessage(ChatRole.User, "/no_think " + query));

    var response = await _chatClient.GetResponseAsync(chatMessages, chatOptions);

    return response.ToString();
}

app.Run();


// -------------------- AI FUNCTIONS --------------------

[Description("Kullanıcının bilgi almak istediği ürün veya ürünleri vector araması yaparak bulup, json formatında geri döner.")]
async Task<string> VectorSearchAsync([Description("ürün adı veya tipi")] string query)
{
    var vector = await _vectorGenerator.GenerateVectorAsync(query);

    VectorSearchOptions<Product> options = new VectorSearchOptions<Product>
    {
       IncludeVectors = true,
       VectorProperty = r => r.Vector,
       //Filter = r => r.Category == "Electronics"

    };
    var results = productsVector.SearchEmbeddingAsync(vector, 5, options);

    var list = new List<ProductSearchResult>();

    await foreach (var d in results)
    {
        if (d.Score > 0.3)//score kontrolü ile en uygun ürünlerin listelenmesi sağlanıyor.
        {
            list.Add(new ProductSearchResult
            {
                Id = d.Record.Id,
                Name = d.Record.Name,
                Description = d.Record.Description,
                Price = d.Record.Price,
                Category = d.Record.Category,
                Barcode = d.Record.Barcode
            });
        }
        
    }

    string jsonResult = JsonSerializer.Serialize(list);

    return jsonResult;
}


[Description("Kullanıcının bilgi almak istediği ürünü, kullanıcının gönderdiği ürün barkod değerine göre bulup ürün bilgilerini döner.")]
async Task<string> ProductSearchWithIdAsync([Description("ürün barkodu")] string barcode)
{
    ////Only connectors for databases that currently support vector plus keyword hybrid search are implementing this interface.
    //IKeywordHybridSearch<Product> productsHybrid = (IKeywordHybridSearch<Product>)_vectorStore.GetCollection<int, Product>("products");//hybrid search için collection oluşturma işlemi yapılıyor.

    //var searchOptions = new HybridSearchOptions<Product>
    //{
    //    IncludeVectors = true,
    //    VectorProperty = r => r.Vector,
    //    AdditionalProperty = r => r.Barcode,
    //};

    //var results = productsHybrid.HybridSearchAsync(string.Empty, [barcode], 1, searchOptions);//vector araması yapmıyoruz bu nedenle vector kısmı boş geçildi;

    var vector = await _vectorGenerator.GenerateVectorAsync(barcode);

    VectorSearchOptions<Product> options = new VectorSearchOptions<Product>
    {
        IncludeVectors = true,
        VectorProperty = r => r.Vector,
        Filter = r => r.Barcode == barcode //barcode'a göre filtreleme yapılıyor

    };
    var results = productsVector.SearchEmbeddingAsync(vector, 5, options);

    var list = new List<ProductSearchResult>();

    await foreach (var d in results)
    {
        if (d.Score > 0.3)//score kontrolü ile en uygun ürünlerin listelenmesi sağlanıyor.
        {
            list.Add(new ProductSearchResult
            {
                Id = d.Record.Id,
                Name = d.Record.Name,
                Description = d.Record.Description,
                Price = d.Record.Price,
                Category = d.Record.Category,
                Barcode = d.Record.Barcode
            });
        }

    }

    string jsonResult = JsonSerializer.Serialize(list);

    return jsonResult;
}