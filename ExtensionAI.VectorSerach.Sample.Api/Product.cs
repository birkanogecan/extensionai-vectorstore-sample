using Microsoft.Extensions.VectorData;

namespace ExtensionAI.VectorSerach.Sample.Api
{
    public record Product
    {
        [VectorStoreRecordKey]
        public int Id { get; init; }

        [VectorStoreRecordData]
        public string Name { get; init; }

        [VectorStoreRecordData]
        public string Description { get; init; }

        [VectorStoreRecordData]
        public decimal Price { get; init; }

        [VectorStoreRecordData(IsIndexed = true)]//searchoption içindeki filtrede kullanmak için işaretleme
        public string Category { get; init; }

        [VectorStoreRecordData(IsFullTextIndexed = true)]//arama yapmak için işaretleme
        public string Barcode { get; init; }

        [VectorStoreRecordVector(1024, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}