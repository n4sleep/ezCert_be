// Neutral crawl contract (AD-8). The processor consumes exactly this shape;
// providers (Firecrawl, Crawlee) translate their output into it.

export interface CrawlRequest {
  url: string;
  limit?: number;
  includePaths?: string[];
}

export interface CrawledDocument {
  canonicalUrl: string;
  title: string;
  markdown: string;
  contentHash: string;
  fetchedAt: string;
  metadata: Record<string, string>;
}

export interface CrawlProvider {
  name: string;
  crawl(req: CrawlRequest): Promise<CrawledDocument[]>;
}
