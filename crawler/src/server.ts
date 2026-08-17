import express from "express";
import type { CrawlProvider } from "./contract.js";
import { FirecrawlProvider } from "./providers/firecrawl.js";
import { validateUrl } from "./safety.js";

// POST /crawl  { url, limit?, includePaths? } -> CrawledDocument[]
// GET  /health -> { status: "ok" }
// The only public surface of the crawler (AD-8). Bearer auth (CRAWLER_SECRET)
// gates every call except /health; URL safety checks (safety.ts) reject
// localhost/private/link-local targets before any provider is invoked.

const app = express();
app.use(express.json());

const expectedSecret = process.env.CRAWLER_SECRET ?? "";

app.use((req, res, next) => {
  if (req.path === "/health") return next();
  const auth = req.headers.authorization ?? "";
  const bearer = auth.startsWith("Bearer ") ? auth.slice(7) : "";
  if (!expectedSecret || bearer !== expectedSecret) {
    return res.status(401).json({ error: "unauthorized" });
  }
  next();
});

app.get("/health", (_req, res) => res.json({ status: "ok", service: "ezcert-crawler" }));

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
  let safeUrl: string;
  try {
    safeUrl = await validateUrl(url);
  } catch (err) {
    return res.status(400).json({ error: err instanceof Error ? err.message : "invalid url" });
  }
  try {
    const docs = await pickProvider().crawl({ url: safeUrl, limit, includePaths });
    return res.json(docs);
  } catch (err) {
    console.error("[crawler] crawl failed:", err);
    return res.status(502).json({ error: "crawl failed" });
  }
});

const port = Number(process.env.PORT ?? 8081);
app.listen(port, () => console.log(`[crawler] listening on :${port}`));
