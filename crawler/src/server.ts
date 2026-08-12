import express from "express";
import type { CrawledDocument, CrawlProvider } from "./contract.js";
import { FirecrawlProvider } from "./providers/firecrawl.js";

// POST /crawl  { url, limit?, includePaths? } -> CrawledDocument[]
// The only public surface of the crawler (AD-8). Deterministic enforcement
// (domains/limits/robots/retries) lives in the provider layer.

const app = express();
app.use(express.json());

function pickProvider(): CrawlProvider {
  const name = process.env.CRAWLER_PROVIDER ?? "firecrawl";
  switch (name) {
    case "firecrawl":
      return new FirecrawlProvider();
    default:
      throw new Error(`Unknown CRAWLER_PROVIDER: ${name}`);
  }
}

app.post("/crawl", async (req, res) => {
  const { url, limit, includePaths } = req.body ?? {};
  if (typeof url !== "string" || !/^https?:\/\//i.test(url)) {
    return res.status(400).json({ error: "url is required and must be http(s)" });
  }
  try {
    const docs: CrawledDocument[] = await pickProvider().crawl({ url, limit, includePaths });
    return res.json(docs);
  } catch (err) {
    console.error("[crawler] crawl failed:", err);
    return res.status(502).json({ error: "crawl failed" });
  }
});

const port = Number(process.env.PORT ?? 8081);
app.listen(port, () => console.log(`[crawler] listening on :${port}`));
