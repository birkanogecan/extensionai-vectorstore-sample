using System.ComponentModel;
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
            new Product { Id = 1, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse with adjustable DPI.", Category = "Electronics", Price = 25.99m },
            new Product { Id = 2, Name = "Gaming Keyboard", Description = "Mechanical keyboard with RGB lighting.", Category = "Electronics", Price = 79.99m },
            new Product { Id = 3, Name = "Bluetooth Speaker", Description = "Portable speaker with high-quality sound.", Category = "Electronics", Price = 49.99m },
            new Product { Id = 4, Name = "Smartphone Stand", Description = "Adjustable stand for smartphones and tablets.", Category = "Accessories", Price = 15.99m },
            new Product { Id = 5, Name = "Noise Cancelling Headphones", Description = "Over-ear headphones with active noise cancellation.", Category = "Electronics", Price = 199.99m },
            new Product { Id = 6, Name = "USB-C Hub", Description = "Multi-port hub with HDMI and USB 3.0 support.", Category = "Accessories", Price = 34.99m },
            new Product { Id = 7, Name = "Fitness Tracker", Description = "Waterproof fitness tracker with heart rate monitor.", Category = "Wearables", Price = 59.99m },
            new Product { Id = 8, Name = "4K Monitor", Description = "27-inch 4K UHD monitor with HDR support.", Category = "Electronics", Price = 299.99m },
            new Product { Id = 9, Name = "External SSD", Description = "1TB portable SSD with fast read/write speeds.", Category = "Storage", Price = 129.99m },
            new Product { Id = 10, Name = "Wireless Charger", Description = "Fast wireless charger compatible with most devices.", Category = "Accessories", Price = 29.99m }
};

var ollamaUri = new Uri("http://localhost:11434/");
IChatClient _chatClient = default!;
var ollamaChatClient = new OllamaChatClient(ollamaUri, "llama3.2");
IEmbeddingGenerator<string, Embedding<float>> _vectorGenerator = new OllamaEmbeddingGenerator(ollamaUri, "snowflake-arctic-embed2");
IVectorStoreRecordCollection<int, Product> productsVector = default!;
var _vectorStore = new InMemoryVectorStore();

productsVector = _vectorStore.GetCollection<int, Product>("products");
await productsVector.CreateCollectionIfNotExistsAsync();

await LoadVectorsIntoStoreAsync(products, _vectorGenerator, productsVector);

_chatClient = new ChatClientBuilder(ollamaChatClient)
    .UseFunctionInvocation()
    .Build();

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

        ToolMode = ChatToolMode.RequireAny,
        Tools =
            [
                AIFunctionFactory.Create(VectorSearchAsync)
            ]
    };

    var chatMessages = new List<ChatMessage>();

    chatMessages.Add(new ChatMessage(ChatRole.System, "Sen bir e‑ticaret asistanısın. ürünler ili ilgili bilgi almak istendiğinde methodu çağırabilirsin, method çağrısı yaptığında sana kullanıcının aramak istediği en yakın ürün bilgisi json formatında dönecek. bu bilgilerin içerisinde ürün ismi, açıklaması, kategorisi ve fiyatı olacak. bu sonucu yorumlayıp, kullanıcının istediği bilgiyi dönebilirsin."));

    chatMessages.Add(new ChatMessage(ChatRole.User, query));

    var response = await _chatClient.GetResponseAsync(chatMessages, chatOptions);

    return response.ToString();
}

app.Run();

async Task LoadVectorsIntoStoreAsync(List<Product> products, IEmbeddingGenerator<string, Embedding<float>> vectorGenerator, IVectorStoreRecordCollection<int, Product> productsVector)
{
    foreach (var product in products)
    {
        product.Vector = await vectorGenerator.GenerateVectorAsync($"{product.Name} {product.Description}");
        await productsVector.UpsertAsync(product);
    }
}

// -------------------- AI FUNCTIONS --------------------

[Description("Kullanıcının metnine göre en uygun ürünü bulup json formatında döner")]
async Task<string> VectorSearchAsync([Description("Arama sorgusu")] string query)
{
    var vector = await _vectorGenerator.GenerateVectorAsync(query);

    var results = productsVector.SearchEmbeddingAsync(vector, 1);

    var list = new List<Product>();
    await foreach (var d in results)
    {
        list.Add(d.Record);
    }

    var product = list.FirstOrDefault();
    string jsonResult = JsonSerializer.Serialize(new { Name = product.Name, Description = product.Description, Price = product.Price, Category = product.Category });

    return jsonResult;
}
