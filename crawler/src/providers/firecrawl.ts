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

// Map Firecrawl REST errors (incl. quota/rate-limit) to a clear message.
async function keylessError(res: Response, ctx: string): Promise<never> {
  let detail = "";
  let code = "";
  try {
    const body = (await res.json()) as { error?: string; code?: string };
    detail = body.error ?? "";
    code = body.code ?? "";
  } catch {
    /* non-JSON error body */
  }
  if (res.status === 429 || res.status === 402 || code === "rate_limit_exceeded" || code === "no_credits" || code === "payment_required") {
    throw new Error("Firecrawl rate limit or quota exceeded — try again later or configure FIRECRAWL_API_KEY.");
  }
  throw new Error(`${ctx} failed (${res.status}${detail ? ": " + detail : ""}).`);
}

// Firecrawl adapter (v1 default). Authenticated SDK mode when FIRECRAWL_API_KEY
// is configured; otherwise Firecrawl's keyless REST API (no Authorization
// header). Preserves the CrawledDocument contract either way.
export class FirecrawlProvider implements CrawlProvider {
  name = "firecrawl";
  private readonly apiKey = process.env.FIRECRAWL_API_KEY;

  async crawl(req: CrawlRequest): Promise<CrawledDocument[]> {
    if (this.apiKey) {
      const { Firecrawl } = await import("firecrawl");
      const client = new Firecrawl({ apiKey: this.apiKey });
      const result = await client.crawl(req.url, {
        limit: req.limit ?? 50,
        scrapeOptions: { formats: ["markdown"] },
        includePaths: req.includePaths,
      });
      return toDocs(result.data ?? [], req.url);
    }
    return this.keylessCrawl(req);
  }

  // Topic discovery: Firecrawl Search returns scraped markdown in the response,
  // so no per-result re-crawl is needed unless deeper site crawling is required.
  async search(req: SearchRequest): Promise<CrawledDocument[]> {
    if (this.apiKey) {
      const { Firecrawl } = await import("firecrawl");
      const client = new Firecrawl({ apiKey: this.apiKey });
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
    return this.keylessSearch(req);
  }

  // ---- Keyless REST mode (no Authorization header) ----

  private async keylessSearch(req: SearchRequest): Promise<CrawledDocument[]> {
    const res = await fetch("https://api.firecrawl.dev/v1/search", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        query: req.topic,
        limit: req.limit ?? 5,
        scrapeOptions: { formats: ["markdown"] },
      }),
    });
    if (!res.ok) return keylessError(res, "Search");
    const body = (await res.json()) as { success?: boolean; data?: Array<{ url?: string; title?: string; markdown?: string; metadata?: { sourceURL?: string; title?: string; statusCode?: number } }> };
    if (!body.success || !Array.isArray(body.data)) throw new Error("Search failed — unexpected Firecrawl response.");
    return body.data
      .map((page) => {
        const url = page.url ?? page.metadata?.sourceURL;
        const markdown = page.markdown ?? "";
        if (!url) return null;
        return {
          canonicalUrl: url,
          title: page.metadata?.title ?? page.title ?? url,
          markdown,
          contentHash: contentHash(markdown),
          fetchedAt: new Date().toISOString(),
          metadata: { statusCode: String(page.metadata?.statusCode ?? "") },
        } as CrawledDocument;
      })
      .filter((d): d is CrawledDocument => d !== null);
  }

  private async keylessCrawl(req: CrawlRequest): Promise<CrawledDocument[]> {
    const start = await fetch("https://api.firecrawl.dev/v1/crawl", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        url: req.url,
        limit: req.limit ?? 50,
        includePaths: req.includePaths,
        scrapeOptions: { formats: ["markdown"] },
      }),
    });
    if (!start.ok) return keylessError(start, "Crawl");
    const started = (await start.json()) as { success?: boolean; id?: string };
    if (!started.success || !started.id) throw new Error("Crawl failed — unexpected Firecrawl response.");
    const jobId = started.id;

    for (let i = 0; i < 60; i++) {
      await new Promise((r) => setTimeout(r, 5000));
      const statusRes = await fetch(`https://api.firecrawl.dev/v1/crawl/${jobId}`);
      if (!statusRes.ok) return keylessError(statusRes, "Crawl status");
      const status = (await statusRes.json()) as { success?: boolean; status?: string; data?: Array<{ markdown?: string; metadata?: { sourceURL?: string; title?: string; statusCode?: number } }> };
      if (status.success === false) throw new Error("Crawl failed — Firecrawl returned an error.");
      if (status.status === "completed") return toDocs(status.data ?? [], req.url);
      if (status.status === "failed" || status.status === "cancelled")
        throw new Error(`Crawl ${status.status} — no usable content.`);
    }
    throw new Error("Crawl timed out — try again shortly.");
  }
}
