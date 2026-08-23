/**
 * SharedUi is compiled from source by the Website build (see the `@vpd/ui` tsconfig paths),
 * but it lives outside the Website folder, so Node/esbuild cannot resolve `@angular/*`
 * from its files. This links SharedUi to the Website's node_modules so every Angular
 * package resolves to a single instance.
 *
 * A tsconfig `paths` mapping (`"@angular/*": ["./node_modules/@angular/*"]`) cannot be
 * used instead: it turns bare package specifiers into file paths, which bypasses the
 * packages' `exports` map. Subpaths such as `@angular/ssr/node` then resolve differently
 * from their package root, producing two copies of the same package at runtime and
 * breaking SSR with "Angular app engine manifest is not set".
 */
import { lstatSync, mkdirSync, readlinkSync, rmSync, symlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const websiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const target = join(websiteRoot, 'node_modules');
const sharedUiRoot = resolve(websiteRoot, '..', 'SharedUi');
const link = join(sharedUiRoot, 'node_modules');
const linkType = process.platform === 'win32' ? 'junction' : 'dir';

function inspectLink() {
  try {
    const stats = lstatSync(link);

    if (!stats.isSymbolicLink()) {
      return { kind: 'directory' };
    }

    return { kind: 'link', target: resolve(sharedUiRoot, readlinkSync(link)) };
  } catch {
    return { kind: 'missing' };
  }
}

const existing = inspectLink();

if (existing.kind === 'directory') {
  console.log(`[link-shared-ui] ${link} is a real directory, leaving it untouched.`);
  process.exit(0);
}

if (existing.kind === 'link' && existing.target === target) {
  process.exit(0);
}

if (existing.kind === 'link') {
  rmSync(link, { recursive: true, force: true });
}

mkdirSync(sharedUiRoot, { recursive: true });
symlinkSync(target, link, linkType);
console.log(`[link-shared-ui] linked ${link} -> ${target}`);
