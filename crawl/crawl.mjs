#!/usr/bin/env node
/**
 * Reusable web-content crawler with site profiles.
 *
 * The crawl is a depth-bounded BFS. A "site profile" decides three things per
 * fetched page: which URL kind it is (origin / group / leaf / ...), what child
 * URLs to follow, and what content to extract. Content with the same `groupKey`
 * is collected into one Markdown file; an `index.md` links them together.
 *
 * Built-in profiles:
 *   - ms-learn : Microsoft Learn learning paths -> modules -> units
 *
 * Usage:
 *   crawl <URL>                              # Windows shim (./crawl.cmd)
 *   node crawl.mjs <URL>                     # bare positional URL
 *   node crawl.mjs --origin <URL>            # legacy flag form
 *   node crawl.mjs <URL> --out-dir ./out
 *   node crawl.mjs <URL> --combined ./single-file.md
 *
 * CLI:
 *   <url>                         Positional. First non-flag arg is treated as --origin.
 *   --origin <url>                Same as positional. One of the two is required.
 *   --profile <name>              Optional. Auto-detected via profile.matches() if omitted.
 *   --out-dir <dir>               Default ./out. Ignored when --combined is set.
 *   --combined <file>             Write one combined Markdown file instead of per-group files.
 *   --max-depth <n>               Default 10. BFS depth cap.
 *   --max-pages <n>               Default 500. Hard cap on total pages fetched.
 *   --max-children <n>            Default 200. Per-page child cap.
 *   --concurrency <n>             Default 1. Concurrent in-flight fetches.
 *   --delay-ms <n>                Default 600. Politeness delay before each fetch.
 *   --same-domain                 Default. Reject children outside origin's hostname.
 *   --no-same-domain              Allow off-site links.
 *   --include-knowledge-checks    MS Learn: include `*-knowledge-check` units.
 *   -h, --help
 *
 * Honors HTTPS_PROXY / HTTP_PROXY environment variables (via undici).
 */

import { writeFileSync, mkdirSync } from "node:fs";
import { resolve, join } from "node:path";
import { setTimeout as sleep } from "node:timers/promises";
import { ProxyAgent, setGlobalDispatcher } from "undici";
import * as cheerio from "cheerio";
import TurndownService from "turndown";

// ---------------------------------------------------------------------------
// shared infrastructure: headers, proxy, fetch, html-to-markdown
// ---------------------------------------------------------------------------

const HEADERS = {
  "User-Agent": "Mozilla/5.0 (compatible; ReusableSiteCrawler/2.0; +https://example.local)",
  "Accept-Language": "en-US,en;q=0.9",
};

const proxy =
  process.env.HTTPS_PROXY || process.env.https_proxy || process.env.HTTP_PROXY || process.env.http_proxy;
if (proxy) setGlobalDispatcher(new ProxyAgent(proxy));

const turndown = new TurndownService({
  headingStyle: "atx",
  codeBlockStyle: "fenced",
  bulletListMarker: "-",
});
turndown.addRule("dropScripts", {
  filter: ["script", "style", "noscript", "svg", "form"],
  replacement: () => "",
});

async function fetchHtml(url) {
  const r = await fetch(url, { headers: HEADERS });
  if (!r.ok) throw new Error(`HTTP ${r.status} ${r.statusText} for ${url}`);
  return await r.text();
}

function htmlElementToMarkdown($el) {
  const html = $el.html() || "";
  let md = turndown.turndown(html).trim();
  // Collapse runs of >2 blank lines to keep output tight.
  return md.replace(/\n{3,}/g, "\n\n");
}

// ---------------------------------------------------------------------------
// profile interface
// ---------------------------------------------------------------------------
//
// A profile is an object with these methods:
//
//   name             : string
//   matches(url)     : bool                          -- auto-detect this profile
//   classify(url)    : string                        -- "path" | "module" | "unit" | ...
//   initialNode(url) : Node                          -- node for the origin URL
//   shouldSkip(url, opts)         : bool             -- drop URL before fetch
//   discoverChildren($, node)     : Node[]           -- enqueue these
//   extractContent($, node)       : Content | null   -- fileLevel or section
//
// A Node has shape: { url, kind, output, depth, sectionIndex?, isOrigin? }
//   - output: { groupKey, fileTitle, fileSourceUrl } | null
//     A node's `output` decides which output file its content (or its
//     descendants' content) lands in. Children inherit unless redirected.
//
// A Content is one of:
//   { fileLevel: true,  fileTitle, fileSourceUrl }   -- sets the group's heading
//   { fileLevel: false, sectionTitle, markdown, sourceUrl }  -- one section in the group
//   null                                              -- nothing extractable
// ---------------------------------------------------------------------------

const msLearnProfile = {
  name: "ms-learn",

  matches(url) {
    try {
      return new URL(url).hostname.endsWith("learn.microsoft.com");
    } catch {
      return false;
    }
  },

  classify(url) {
    const p = new URL(url).pathname;
    if (/\/training\/paths\/[^/]+\/?$/i.test(p)) return "path";
    if (/\/training\/modules\/[^/]+\/?$/i.test(p)) return "module";
    if (/\/training\/modules\/[^/]+\/[^/]+\/?$/i.test(p)) return "unit";
    return "unknown";
  },

  initialNode(url) {
    const kind = this.classify(url);
    let output = null;
    if (kind === "module" || kind === "unit") {
      // If the origin is itself a module/unit, treat it as its own group so
      // a single output file can be produced even without a path layer.
      const slug = slugFromUrl(url);
      output = { groupKey: slug, fileTitle: null, fileSourceUrl: url };
    }
    return { url, kind, output, depth: 0, isOrigin: true };
  },

  shouldSkip(url, opts) {
    if (!opts.includeKnowledgeChecks && /\/\d+-knowledge-check\/?$/i.test(new URL(url).pathname)) {
      return true;
    }
    return false;
  },

  discoverChildren($, node) {
    const children = [];
    if (node.kind === "path") {
      const seen = new Set();
      $('a[href*="/modules/"]').each((_, el) => {
        const href = $(el).attr("href");
        if (!href) return;
        let abs;
        try {
          abs = new URL(href, node.url);
        } catch {
          return;
        }
        abs.hash = "";
        abs.search = "";
        const m = abs.pathname.match(/^(\/[a-z-]+\/training\/modules\/[a-z0-9-]+)\/?$/i);
        if (!m) return;
        abs.pathname = m[1] + "/";
        const key = abs.href;
        if (seen.has(key)) return;
        seen.add(key);
        const slug = m[1].split("/").pop();
        children.push({
          url: key,
          kind: "module",
          output: { groupKey: slug, fileTitle: null, fileSourceUrl: key },
          depth: node.depth + 1,
        });
      });
    } else if (node.kind === "module") {
      const seen = new Set();
      $("#unit-list a[href]").each((_, el) => {
        const href = $(el).attr("href");
        if (!href) return;
        let abs;
        try {
          abs = new URL(href, node.url);
        } catch {
          return;
        }
        abs.hash = "";
        abs.search = "";
        const key = abs.href;
        if (seen.has(key) || abs.href === node.url) return;
        seen.add(key);
        children.push({
          url: key,
          kind: "unit",
          output: node.output, // units always go into the parent module's group
          depth: node.depth + 1,
          sectionIndex: children.length,
        });
      });
    }
    return children;
  },

  extractContent($, node) {
    if (node.kind === "path") {
      const title = $("h1").first().text().trim() || "Learning Path";
      return { fileLevel: true, fileTitle: title, fileSourceUrl: node.url };
    }
    if (node.kind === "module") {
      const title = $("h1").first().text().trim() || node.url;
      return { fileLevel: true, fileTitle: title, fileSourceUrl: node.url };
    }
    if (node.kind === "unit") {
      const title = $("#module-unit-title, h1").first().text().trim() || node.url;
      const body = $("#module-unit-content").first();
      if (!body.length) {
        return {
          fileLevel: false,
          sectionTitle: title,
          markdown: "[unavailable: #module-unit-content not found]",
          sourceUrl: node.url,
        };
      }
      body.find("[data-bi-name='feedback'], .feedback-section, .next-and-previous, button").remove();
      return {
        fileLevel: false,
        sectionTitle: title,
        markdown: htmlElementToMarkdown(body),
        sourceUrl: node.url,
      };
    }
    return null;
  },
};

const PROFILES = [msLearnProfile];

function pickProfile(url, name) {
  if (name) {
    const p = PROFILES.find((x) => x.name === name);
    if (!p) {
      const known = PROFILES.map((x) => x.name).join(", ");
      throw new Error(`Unknown profile "${name}". Known: ${known}`);
    }
    return p;
  }
  const p = PROFILES.find((x) => x.matches(url));
  if (!p) {
    const known = PROFILES.map((x) => x.name).join(", ");
    throw new Error(`No profile matches ${url}. Pass --profile explicitly. Known: ${known}`);
  }
  return p;
}

function slugFromUrl(url) {
  try {
    const u = new URL(url);
    const segs = u.pathname.split("/").filter(Boolean);
    return segs[segs.length - 1] || u.hostname.replace(/[^a-z0-9]+/gi, "-");
  } catch {
    return "page";
  }
}

// ---------------------------------------------------------------------------
// output aggregator
// ---------------------------------------------------------------------------

class OutputAggregator {
  constructor() {
    this.origin = null; // { title, sourceUrl }
    this.groups = new Map(); // groupKey -> { title, sourceUrl, sections: Section[] }
    this.groupOrder = [];
  }
  setOrigin(title, sourceUrl) {
    this.origin = { title, sourceUrl };
  }
  upsertGroup(key, title, sourceUrl) {
    if (!this.groups.has(key)) {
      this.groups.set(key, { title: title || null, sourceUrl: sourceUrl || null, sections: [] });
      this.groupOrder.push(key);
    } else {
      const g = this.groups.get(key);
      if (title) g.title = title;
      if (sourceUrl) g.sourceUrl = sourceUrl;
    }
  }
  addSection(key, section) {
    if (!this.groups.has(key)) {
      this.groups.set(key, { title: null, sourceUrl: null, sections: [] });
      this.groupOrder.push(key);
    }
    this.groups.get(key).sections.push(section);
  }
  totalSections() {
    let n = 0;
    for (const k of this.groupOrder) n += this.groups.get(k).sections.length;
    return n;
  }
}

// ---------------------------------------------------------------------------
// BFS crawler
// ---------------------------------------------------------------------------

async function crawl(profile, origin, opts) {
  const out = new OutputAggregator();
  const failures = [];
  const visited = new Set();
  const queue = [profile.initialNode(origin)];
  let pagesFetched = 0;
  let totalChildrenSeen = 0;

  let originHost = "";
  try {
    originHost = new URL(origin).hostname;
  } catch {
    throw new Error(`Invalid --origin URL: ${origin}`);
  }

  const inflight = new Set();

  async function processNode(node) {
    if (visited.has(node.url)) return;
    visited.add(node.url);

    if (profile.shouldSkip && profile.shouldSkip(node.url, opts)) {
      console.log(`         skip: ${node.url}`);
      return;
    }
    if (opts.sameDomain) {
      try {
        if (new URL(node.url).hostname !== originHost) return;
      } catch {
        return;
      }
    }
    if (node.depth > opts.maxDepth) return;
    if (pagesFetched >= opts.maxPages) return;

    pagesFetched++;
    const idx = pagesFetched;
    console.log(`[${idx}] depth=${node.depth} kind=${node.kind} ${node.url}`);

    if (opts.delayMs) await sleep(opts.delayMs);

    let html;
    try {
      html = await fetchHtml(node.url);
    } catch (err) {
      failures.push({ url: node.url, error: err.message });
      console.log(`         FAIL: ${err.message}`);
      return;
    }
    const $ = cheerio.load(html);

    const content = profile.extractContent($, node);
    if (content) {
      if (content.fileLevel) {
        if (node.isOrigin) out.setOrigin(content.fileTitle, content.fileSourceUrl);
        if (node.output) out.upsertGroup(node.output.groupKey, content.fileTitle, content.fileSourceUrl);
      } else if (node.output) {
        out.addSection(node.output.groupKey, {
          title: content.sectionTitle,
          markdown: content.markdown,
          sourceUrl: content.sourceUrl,
          index: typeof node.sectionIndex === "number" ? node.sectionIndex : Number.MAX_SAFE_INTEGER,
        });
      }
    }

    if (node.depth < opts.maxDepth) {
      const children = profile.discoverChildren($, node) || [];
      const limited = children.slice(0, opts.maxChildren);
      totalChildrenSeen += limited.length;
      for (const c of limited) {
        if (!visited.has(c.url)) queue.push(c);
      }
    }
  }

  while (queue.length || inflight.size > 0) {
    while (inflight.size < opts.concurrency && queue.length) {
      const node = queue.shift();
      const p = processNode(node).finally(() => inflight.delete(p));
      inflight.add(p);
    }
    if (inflight.size) await Promise.race(inflight);
  }

  // Stable section order: BFS arrival is non-deterministic with concurrency>1.
  for (const key of out.groupOrder) {
    out.groups.get(key).sections.sort((a, b) => a.index - b.index);
  }

  return { out, failures, pagesFetched, totalChildrenSeen };
}

// ---------------------------------------------------------------------------
// renderers
// ---------------------------------------------------------------------------

function renderGroupFile(origin, group) {
  const lines = [];
  lines.push(`# ${group.title || "(untitled)"}`);
  lines.push("");
  if (group.sourceUrl) lines.push(`> Source: ${group.sourceUrl}  `);
  if (origin?.sourceUrl) lines.push(`> Origin: ${origin.sourceUrl}  `);
  lines.push(`> Generated: ${new Date().toISOString()}`);
  lines.push("");
  lines.push("---");
  lines.push("");
  group.sections.forEach((s, i) => {
    lines.push(`## ${i + 1}. ${s.title}`);
    lines.push("");
    lines.push(`Source: ${s.sourceUrl}`);
    lines.push("");
    lines.push(s.markdown);
    lines.push("");
  });
  return lines.join("\n");
}

function renderIndexFile(origin, groups, groupOrder) {
  const lines = [];
  lines.push(`# ${origin?.title || "Crawl Index"}`);
  lines.push("");
  if (origin?.sourceUrl) lines.push(`> Origin: ${origin.sourceUrl}  `);
  lines.push(`> Generated: ${new Date().toISOString()}`);
  lines.push("");
  lines.push("---");
  lines.push("");
  lines.push(`## Groups (${groupOrder.length})`);
  lines.push("");
  for (const key of groupOrder) {
    const g = groups.get(key);
    const file = `${key}.md`;
    const src = g.sourceUrl ? ` — [source](${g.sourceUrl})` : "";
    lines.push(`- [${g.title || key}](./${file}) — ${g.sections.length} sections${src}`);
  }
  lines.push("");
  return lines.join("\n");
}

function renderCombined(origin, groups, groupOrder) {
  const lines = [];
  lines.push(`# ${origin?.title || "Crawl Result"}`);
  lines.push("");
  if (origin?.sourceUrl) lines.push(`> Source: ${origin.sourceUrl}  `);
  lines.push(`> Generated: ${new Date().toISOString()}`);
  lines.push("");
  lines.push("---");
  lines.push("");
  let groupNum = 0;
  for (const key of groupOrder) {
    groupNum++;
    const g = groups.get(key);
    lines.push(`## Group ${groupNum}: ${g.title || key}`);
    lines.push("");
    if (g.sourceUrl) lines.push(`Source: ${g.sourceUrl}`);
    lines.push("");
    g.sections.forEach((s, i) => {
      lines.push(`### Section ${i + 1}: ${s.title}`);
      lines.push("");
      lines.push(`Source: ${s.sourceUrl}`);
      lines.push("");
      lines.push(s.markdown);
      lines.push("");
    });
  }
  return lines.join("\n");
}

// ---------------------------------------------------------------------------
// cli + main
// ---------------------------------------------------------------------------

const DEFAULT_ORIGIN =
  "https://learn.microsoft.com/en-us/training/paths/microsoft-azure-fundamentals-describe-cloud-concepts/";

const DEFAULTS = {
  origin: null,
  profile: null,
  outDir: "out",
  combined: null,
  maxDepth: 10,
  maxPages: 500,
  maxChildren: 200,
  concurrency: 1,
  delayMs: 600,
  sameDomain: true,
  includeKnowledgeChecks: false,
};

function printHelp() {
  console.log(
    [
      "Usage:",
      "  crawl <URL> [flags]                        # Windows shim (./crawl.cmd)",
      "  node crawl.mjs <URL> [flags]               # bare positional URL",
      "  node crawl.mjs --origin <URL> [flags]      # legacy flag form",
      "  node crawl.mjs                             # no args -> default MS Learn path",
      "",
      "Flags:",
      "  --profile <name>           pick site profile (auto-detected if omitted)",
      "  --out-dir <dir>            output dir for per-group .md files (default ./out)",
      "  --combined <file>          write a single combined .md instead",
      "  --max-depth <n>            BFS depth cap (default 10)",
      "  --max-pages <n>            total page cap (default 500)",
      "  --max-children <n>         per-page child cap (default 200)",
      "  --concurrency <n>          concurrent fetches (default 1)",
      "  --delay-ms <n>             politeness delay before each fetch (default 600)",
      "  --same-domain              stay on origin's hostname (default)",
      "  --no-same-domain           allow off-site links",
      "  --include-knowledge-checks MS Learn: include *-knowledge-check units",
      "  -h, --help                 show this help",
      "",
      "Profiles: " + PROFILES.map((p) => p.name).join(", "),
      "",
      "Examples:",
      "  crawl https://learn.microsoft.com/en-us/training/paths/<slug>/",
      "  crawl https://learn.microsoft.com/en-us/training/modules/<slug>/ --out-dir ./crawls/foo",
      "  node crawl.mjs https://learn.microsoft.com/.../paths/<slug>/ --combined out.md",
    ].join("\n"),
  );
}

function parseArgs(argv) {
  const opts = { ...DEFAULTS };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    switch (a) {
      case "--origin": opts.origin = argv[++i]; break;
      case "--profile": opts.profile = argv[++i]; break;
      case "--out-dir": opts.outDir = argv[++i]; break;
      case "--combined": opts.combined = argv[++i]; break;
      case "--max-depth": opts.maxDepth = Number(argv[++i]); break;
      case "--max-pages": opts.maxPages = Number(argv[++i]); break;
      case "--max-children": opts.maxChildren = Number(argv[++i]); break;
      case "--concurrency": opts.concurrency = Math.max(1, Number(argv[++i])); break;
      case "--delay-ms": opts.delayMs = Number(argv[++i]); break;
      case "--same-domain": opts.sameDomain = true; break;
      case "--no-same-domain": opts.sameDomain = false; break;
      case "--include-knowledge-checks": opts.includeKnowledgeChecks = true; break;
      case "-h":
      case "--help": printHelp(); process.exit(0);
      default:
        // First non-flag arg is treated as the origin URL (positional shorthand).
        if (!a.startsWith("-") && !opts.origin && /^https?:\/\//i.test(a)) {
          opts.origin = a;
        } else {
          console.error("Unknown arg:", a);
          printHelp();
          process.exit(2);
        }
    }
  }
  if (!opts.origin) {
    opts.origin = DEFAULT_ORIGIN;
    console.log(`[crawl] no URL given; defaulting to ${opts.origin}`);
  }
  return opts;
}

async function main() {
  const opts = parseArgs(process.argv);
  const start = Date.now();
  const profile = pickProfile(opts.origin, opts.profile);

  console.log(`[crawl] origin     : ${opts.origin}`);
  console.log(`[crawl] profile    : ${profile.name}`);
  console.log(`[crawl] depth/pages: ${opts.maxDepth} / ${opts.maxPages}`);
  console.log(`[crawl] concurrency: ${opts.concurrency} | delay: ${opts.delayMs}ms`);
  console.log(`[crawl] same-domain: ${opts.sameDomain}`);
  if (proxy) console.log(`[crawl] proxy      : ${proxy}`);
  console.log("");

  const { out, failures, pagesFetched, totalChildrenSeen } = await crawl(profile, opts.origin, opts);

  if (opts.combined) {
    const md = renderCombined(out.origin, out.groups, out.groupOrder);
    const outPath = resolve(opts.combined);
    writeFileSync(outPath, md, "utf8");
    console.log("");
    console.log(`[crawl] wrote combined: ${outPath}`);
  } else {
    const dir = resolve(opts.outDir);
    mkdirSync(dir, { recursive: true });
    for (const key of out.groupOrder) {
      const g = out.groups.get(key);
      writeFileSync(join(dir, `${key}.md`), renderGroupFile(out.origin, g), "utf8");
    }
    writeFileSync(join(dir, "index.md"), renderIndexFile(out.origin, out.groups, out.groupOrder), "utf8");
    console.log("");
    console.log(`[crawl] wrote: ${out.groupOrder.length + 1} files in ${dir}/`);
  }

  const elapsed = ((Date.now() - start) / 1000).toFixed(1);
  console.log(`[crawl] origin title : ${out.origin?.title ?? "(none)"}`);
  console.log(
    `[crawl] groups: ${out.groupOrder.length} | sections: ${out.totalSections()} | pages fetched: ${pagesFetched} | children seen: ${totalChildrenSeen} | failures: ${failures.length}`,
  );
  for (const k of out.groupOrder) {
    const g = out.groups.get(k);
    console.log(`         - ${k}: ${g.title ?? "(untitled)"} (${g.sections.length} sections)`);
  }
  if (failures.length) {
    console.log("[crawl] failures:");
    for (const f of failures) console.log(`         - ${f.url} :: ${f.error}`);
  }
  console.log(`[crawl] done in ${elapsed}s`);
}

main().catch((err) => {
  console.error("[crawl] fatal:", err);
  process.exit(1);
});
