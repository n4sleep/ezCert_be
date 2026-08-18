import express from "express";
import type { CrawlProvider } from "./contract.js";
import { FirecrawlProvider } from "./providers/firecrawl.js";
import { validateUrl, assertSafeUrl } from "./safety.js";

// POST /crawl   { url, limit?, includePaths? } -> CrawledDocument[]
// POST /search  { topic, limit? }               -> CrawledDocument[]
// GET  /health  -> { status: "ok" }
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

app.post("/search", async (req, res) => {
  const { topic, limit } = req.body ?? {};
  if (typeof topic !== "string" || topic.trim().length === 0) {
    return res.status(400).json({ error: "topic is required" });
  }
  try {
    const docs = await pickProvider().search({ topic: topic.trim(), limit });
    // Defense in depth: even though Firecrawl fetches server-side, reject any
    // result that fails the URL safety gate (scheme/loopback/private/link-local).
    const safe: Array<{ doc: (typeof docs)[number] }> = [];
    for (const doc of docs) {
      try {
        await assertSafeUrl(doc.canonicalUrl);
        safe.push({ doc });
      } catch {
        console.warn("[crawler] search result rejected (unsafe URL):", doc.canonicalUrl);
      }
    }
    return res.json(safe.map((s) => s.doc));
  } catch (err) {
    console.error("[crawler] search failed:", err);
    return res.status(502).json({ error: "search failed" });
  }
});

const port = Number(process.env.PORT ?? 8081);
app.listen(port, () => console.log(`[crawler] listening on :${port}`));
