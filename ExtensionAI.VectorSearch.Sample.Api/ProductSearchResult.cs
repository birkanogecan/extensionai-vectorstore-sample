using Microsoft.Extensions.VectorData;

namespace ExtensionAI.VectorSerach.Sample.Api
{
    public record ProductSearchResult
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public decimal Price { get; init; }
        public string Category { get; init; }
        public string Barcode { get; init; }
      
    }
}
