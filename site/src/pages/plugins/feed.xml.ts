// mazapan.dev/plugins/feed.xml: what's new in the plugin registry, as Atom:
// each plugin's latest version, newest first.
import { generated, plugins } from "../../lib/plugins";

const escape = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]!);

export function GET() {
  const site = "https://mazapan.dev";
  const entries = plugins.map((p) => {
    const link = `${site}/plugins/${p.id}/`;
    const isNew = p.added === p.updated;
    return `  <entry>
    <id>${link}#${p.version}</id>
    <title>${escape(isNew ? `New: ${p.name} ${p.version}` : `${p.name} ${p.version}`)}</title>
    <link href="${link}"/>
    <updated>${p.updated}</updated>
    <published>${p.updated}</published>
    <author><name>${escape(p.author)}</name></author>
    <summary>${escape(p.description)}</summary>
  </entry>`;
  });
  const body = `<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <id>${site}/plugins/</id>
  <title>Mazapan plugins</title>
  <subtitle>New plugins and new versions in the Mazapan plugin registry</subtitle>
  <link href="${site}/plugins/"/>
  <link rel="self" href="${site}/plugins/feed.xml"/>
  <updated>${generated}</updated>
${entries.join("\n")}
</feed>
`;
  return new Response(body, { headers: { "Content-Type": "application/atom+xml; charset=utf-8" } });
}
