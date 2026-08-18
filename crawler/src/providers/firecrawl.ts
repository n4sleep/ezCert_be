import type { CrawledDocument, CrawlProvider, CrawlRequest, SearchRequest } from "../contract.js";

function contentHash(text: string): string {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return (h >>> 0).toString(16);
}

function toDocs(entries: Array<{ markdown?: string; metadata?: { sourceURL?: string; title?: string; statusCode?: number } }>, fallbackUrl: string): CrawledDocument[] {
  const docs: CrawledDocument[] = [];
  for (const page of entries ?? []) {
    const markdown = page.markdown ?? "";
    const canonicalUrl = page.metadata?.sourceURL ?? fallbackUrl;
    docs.push({
      canonicalUrl,
      title: page.metadata?.title ?? canonicalUrl,
      markdown,
      contentHash: contentHash(markdown),
      fetchedAt: new Date().toISOString(),
      metadata: { statusCode: String(page.metadata?.statusCode ?? "") },
    });
  }
  return docs;
}

function requireKey(): string {
  const apiKey = process.env.FIRECRAWL_API_KEY;
  if (!apiKey) throw new Error("FIRECRAWL_API_KEY is not configured");
  return apiKey;
}

// Firecrawl adapter (v1 default). Uses FIRECRAWL_API_KEY from env; the hosted
// API performs sitemap discovery, JS rendering, search, and rate limiting.
export class FirecrawlProvider implements CrawlProvider {
  name = "firecrawl";

  async crawl(req: CrawlRequest): Promise<CrawledDocument[]> {
    const { Firecrawl } = await import("firecrawl");
    const client = new Firecrawl({ apiKey: requireKey() });

    const result = await client.crawl(req.url, {
      limit: req.limit ?? 50,
      scrapeOptions: { formats: ["markdown"] },
      includePaths: req.includePaths,
    });

    return toDocs(result.data ?? [], req.url);
  }

  // Topic discovery (WS-3B): Firecrawl Search returns scraped markdown content
  // in the search response, so no per-result re-crawl is needed unless deeper
  // site crawling is required later.
  async search(req: SearchRequest): Promise<CrawledDocument[]> {
    const { Firecrawl } = await import("firecrawl");
    const client = new Firecrawl({ apiKey: requireKey() });

    const result = await client.search(req.topic, {
      limit: req.limit ?? 5,
      scrapeOptions: { formats: ["markdown"] },
    });

    const docs: CrawledDocument[] = [];
    for (const group of [result.web, result.developer, result.news] as Array<
      Array<{ url?: string; metadata?: { sourceURL?: string; title?: string; statusCode?: number }; markdown?: string }> | undefined
    >) {
      for (const page of group ?? []) {
        const url = page.url ?? page.metadata?.sourceURL;
        const markdown = page.markdown ?? "";
        if (!url) continue;
        docs.push({
          canonicalUrl: url,
          title: page.metadata?.title ?? url,
          markdown,
          contentHash: contentHash(markdown),
          fetchedAt: new Date().toISOString(),
          metadata: { statusCode: String(page.metadata?.statusCode ?? "") },
        });
      }
    }
    return docs;
  }
}
