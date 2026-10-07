// The plugin registry's plugins, as scripts/registry.mjs brought them, and
// what the gallery's pages need from them.
import { Marked, type Tokens } from "marked";
import registry from "../data/plugins.json";

export interface Plugin {
  id: string;
  name: string;
  description: string;
  version: string;
  author: string;
  homepage: string;
  license: string;
  source: string;
  ref: string;
  commit: string;
  categories: string[];
  requires: string[];
  capabilities: string[];
  settings: number;
  languages: string[];
  translations: Record<string, { name?: string; description?: string }>;
  icon: string;
  screenshots: string[];
  readme: string;
  pictures?: string[];
  added: string;
  updated: string;
}

export const generated: string = registry.generated;

/**
 * The first Mazapan that reads the registry: "" until it's out. Set it in
 * the release that brings it (docs/versioning.md): the gallery then says
 * which Mazapan its plugins need instead of "the next one".
 */
export const since: string = "";
export const needs = since
  ? `Needs Mazapan ${since} or newer.`
  : "Coming with the next Mazapan release (after 0.3.2): until then, Mazapan built from main.";

/** The guide to publishing one, step by step. */
export const guide = "https://github.com/rick-dev-creator/mazapan/blob/main/docs/publishing-plugins.md";
/** Newest first: what changed lately leads. */
export const plugins: Plugin[] = [...(registry.plugins as Plugin[])].sort((a, b) => b.updated.localeCompare(a.updated) || a.name.localeCompare(b.name));

export const categoryNames: Record<string, string> = {
  bar: "Bar", panel: "Panels", theme: "Themes", window: "Windows", hardware: "Hardware", tools: "Tools", agent: "Agents",
};

export const languageNames: Record<string, string> = {
  en: "English", es: "Español", de: "Deutsch", fr: "Français", pt: "Português",
};

/** A picture of the registry's, as the site serves it. */
export const media = (path: string) => `/plugins/${path}`;

export const date = (iso: string) =>
  new Date(iso).toLocaleDateString("en", { year: "numeric", month: "short", day: "numeric", timeZone: "UTC" });

const escape = (s: string) =>
  s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!);

/** On GitHub: owner/repo, for links into the repository at the commit listed. */
const github = (source: string) => /^https:\/\/github\.com\/([^/]+\/[^/]+)$/.exec(source)?.[1];

/**
 * A plugin's README as HTML, safe to put in the page: it's someone else's
 * text. Its HTML is shown as text, links go only to http(s) and mail, and
 * paths in the repository point to it at the commit listed.
 */
export function readme(p: Plugin): string {
  const repo = github(p.source);
  const resolve = (href: string, raw: boolean): string | null => {
    if (/^(https?:|mailto:)/i.test(href)) return href;
    if (href.startsWith("#")) return href;
    if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith("//")) return null; // javascript:, data:, …
    if (!repo) return null;
    const path = href.replace(/^\.?\//, "");
    return raw ? `https://raw.githubusercontent.com/${repo}/${p.commit}/${path}` : `https://github.com/${repo}/blob/${p.commit}/${path}`;
  };
  const md = new Marked({
    gfm: true,
    renderer: {
      html: (t: Tokens.HTML | Tokens.Tag) => escape(t.text),
      link(t: Tokens.Link) {
        const inner = this.parser.parseInline(t.tokens);
        const href = resolve(t.href, false);
        return href ? `<a href="${escape(href)}" rel="nofollow noopener">${inner}</a>` : inner;
      },
      image(t: Tokens.Image) {
        // The gallery's own pictures are on the page already.
        if (p.pictures?.includes(t.href.replace(/^\.?\//, ""))) return "";
        const src = resolve(t.href, true);
        return src && /^https:/.test(src) ? `<img src="${escape(src)}" alt="${escape(t.text)}" loading="lazy" />` : escape(t.text);
      },
      // The page has its own title: the README's first heading would say it twice.
      heading(t: Tokens.Heading) {
        const level = Math.min(t.depth + 1, 6);
        return `<h${level}>${this.parser.parseInline(t.tokens)}</h${level}>\n`;
      },
    },
  });
  // The README's own title and the screenshot under it are the page's header already.
  const body = p.readme.replace(/^#\s+.*\n+/, "");
  return md.parse(body, { async: false }) as string;
}
