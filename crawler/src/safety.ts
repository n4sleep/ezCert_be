import dns from "node:dns/promises";
import { URL } from "node:url";

// URL safety gate (SSRF hardening): bearer auth stops unauthorized use but
// does not stop a legit user pointing the crawler at internal targets.
// Reject unsafe schemes, loopback/link-local/private IPs, and hostnames that
// resolve to them. Redirect hops must be re-validated per hop.

const BLOCKED_IP_PATTERNS: Array<[string, number]> = [
  // [cidr, prefixBits] — only IPv4 here; IPv6 loopback/link-local handled below.
  ["0.0.0.0", 8],
  ["10.0.0.0", 8],
  ["100.64.0.0", 10],
  ["127.0.0.0", 8],
  ["169.254.0.0", 16],
  ["172.16.0.0", 12],
  ["192.168.0.0", 16],
  ["198.18.0.0", 15],
  ["224.0.0.0", 4],
  ["240.0.0.0", 4],
];

function ipv4ToInt(ip: string): number {
  return ip.split(".").reduce((acc, octet) => (acc << 8) | Number(octet), 0) >>> 0;
}

function isBlockedIPv4(ip: string): boolean {
  const addr = ipv4ToInt(ip);
  return BLOCKED_IP_PATTERNS.some(([cidr, bits]) => {
    const mask = bits === 0 ? 0 : (0xffffffff << (32 - bits)) >>> 0;
    const base = ipv4ToInt(cidr);
    return (addr & mask) === (base & mask);
  });
}

function isBlockedIPv6(ip: string): boolean {
  const lower = ip.toLowerCase();
  return lower === "::1" || lower === "::" || lower.startsWith("fe80:");
}

async function isSafeHost(hostname: string): Promise<boolean> {
  if (hostname === "localhost" || hostname.endsWith(".localhost")) return false;
  let addrs: string[];
  try {
    addrs = (await dns.lookup(hostname, { all: true })).map((a) => a.address);
  } catch {
    return false; // unresolvable -> reject
  }
  if (addrs.length === 0) return false;
  return addrs.every((ip) => {
    if (ip.includes(":")) return !isBlockedIPv6(ip);
    return !isBlockedIPv4(ip);
  });
}

// Throws an Error with a friendly message when the URL is not safe to crawl.
export async function assertSafeUrl(raw: string): Promise<void> {
  let parsed: URL;
  try {
    parsed = new URL(raw);
  } catch {
    throw new Error("url must be a valid absolute URL");
  }
  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    throw new Error("url must use http or https");
  }
  if (!(await isSafeHost(parsed.hostname))) {
    throw new Error("url resolves to a blocked address (localhost/private/link-local)");
  }
}

// Validate a URL and return the canonical string; throws when unsafe.
export async function validateUrl(raw: unknown): Promise<string> {
  if (typeof raw !== "string" || !/^https?:\/\//i.test(raw)) {
    throw new Error("url is required and must be http(s)");
  }
  await assertSafeUrl(raw);
  return raw;
}
