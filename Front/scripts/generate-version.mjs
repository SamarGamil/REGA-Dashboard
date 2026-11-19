// Generates src/version.ts at build time: package version + git short-SHA + UTC build date.
// Safe fallback to "local" when git is unavailable (CI/docker without a repo).
import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

let commit = 'local';
try {
  commit = execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] })
    .toString()
    .trim();
} catch {
  // git not available — keep fallback
}

const buildDate = new Date().toISOString().replace(/\.\d+Z$/, 'Z');

const contents = `// AUTO-GENERATED at build time by scripts/generate-version.mjs. Do not edit by hand.
export const APP_VERSION = {
  version: '${pkg.version}',
  commit: '${commit}',
  buildDate: '${buildDate}',
} as const;
`;

writeFileSync(join(root, 'src', 'version.ts'), contents);
console.log(`Generated src/version.ts -> v${pkg.version} ${commit} ${buildDate}`);
