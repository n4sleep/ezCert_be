import type { CrawledDocument, CrawlProvider, CrawlRequest } from "../contract.js";

function contentHash(text: string): string {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return (h >>> 0).toString(16);
}

// Firecrawl adapter (v1 default). Uses FIRECRAWL_API_KEY from env when set;
// the hosted API performs sitemap discovery, JS rendering, and rate limiting.
export class FirecrawlProvider implements CrawlProvider {
  name = "firecrawl";

  async crawl(req: CrawlRequest): Promise<CrawledDocument[]> {
    const { Firecrawl } = await import("firecrawl");
    const apiKey = process.env.FIRECRAWL_API_KEY;
    const client = new Firecrawl(apiKey ? { apiKey } : undefined);

    const result = await client.crawl(req.url, {
      limit: req.limit ?? 50,
      scrapeOptions: { formats: ["markdown"] },
      includePaths: req.includePaths,
    });

    const docs: CrawledDocument[] = [];
    for (const page of result.data ?? []) {
      const markdown = page.markdown ?? "";
      docs.push({
        canonicalUrl: page.metadata?.sourceURL ?? req.url,
        title: page.metadata?.title ?? req.url,
        markdown,
        contentHash: contentHash(markdown),
        fetchedAt: new Date().toISOString(),
        metadata: { statusCode: String(page.metadata?.statusCode ?? "") },
      });
    }
    return docs;
  }
}
