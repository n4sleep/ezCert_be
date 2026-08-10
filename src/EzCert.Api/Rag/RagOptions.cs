namespace EzCert.Api.Rag;

// Configuration for the RAG pipeline (Bedrock + Qdrant).
// Bound from the "Rag" section of appsettings; AWS credentials come from the
// default AWS credential chain (env vars), NOT from config, to keep secrets out of the repo.
public sealed class RagOptions
{
    public bool Enabled { get; set; } = true;

    // AWS Bedrock
    public string Region { get; set; } = "us-east-1";
    public string EmbeddingModel { get; set; } = "amazon.titan-embed-text-v2:0";
    public string GenerationModel { get; set; } = "amazon.nova-micro-v1:0";
    public int EmbeddingDim { get; set; } = 1024;

    // Qdrant (gRPC)
    public string QdrantHost { get; set; } = "localhost";
    public int QdrantPort { get; set; } = 6334;
    public bool QdrantHttps { get; set; } = false;
    public string Collection { get; set; } = "ezcert_chunks";

    // Chunking
    public int MaxChunkChars { get; set; } = 1500;
    public int SearchLimit { get; set; } = 5;

    // Source content. Relative paths are resolved by walking up from the content root
    // until a "crawl/out" directory is found.
    public string CrawlPath { get; set; } = "crawl/out";

    // Maps crawled markdown filenames (without extension) to exam section slugs.
    public Dictionary<string, string> SlugMap { get; set; } = new()
    {
        ["describe-cloud-compute"] = "cloud-computing",
        ["describe-cloud-service-types"] = "service-types",
        ["describe-benefits-use-cloud-services"] = "benefits"
    };
}
