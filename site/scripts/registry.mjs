// The plugin registry, before the site is built: its generated/ folder
// (index.toml, plugins.json, media/) from github.com/rick-dev-creator/mazapan-plugins,
// or from $REGISTRY_DIR (a checkout of it), into public/plugins/ (served as
// they are: Mazapan reads mazapan.dev/plugins/index.toml) and src/data/ (the
// gallery's pages are made from it). No registry, no build: a site without
// its catalog must never replace the one that's up.
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const site = join(dirname(fileURLToPath(import.meta.url)), "..");
const repo = "https://github.com/rick-dev-creator/mazapan-plugins";

let dir = process.env.REGISTRY_DIR;
let tmp;
if (!dir) {
  tmp = mkdtempSync(join(tmpdir(), "mazapan-registry-"));
  dir = join(tmp, "registry");
  execFileSync("git", ["clone", "--quiet", "--depth=1", repo, dir], { stdio: "inherit" });
}
const generated = join(dir, "generated");
const data = JSON.parse(readFileSync(join(generated, "plugins.json"), "utf8"));
if (!Array.isArray(data.plugins) || !existsSync(join(generated, "index.toml"))) {
  throw new Error(`${generated}: no plugins.json or index.toml`);
}

const out = join(site, "public", "plugins");
rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
for (const f of ["index.toml", "plugins.json", "media"]) cpSync(join(generated, f), join(out, f), { recursive: true });
mkdirSync(join(site, "src", "data"), { recursive: true });
writeFileSync(join(site, "src", "data", "plugins.json"), JSON.stringify(data));
if (tmp) rmSync(tmp, { recursive: true, force: true });
console.log(`plugin registry: ${data.plugins.length} plugins (${data.generated})`);
