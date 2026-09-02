/**
 * SharedUi is compiled from source by each Angular application (see the `@vpd/ui`
 * tsconfig paths), but it lives outside the application folder. Node/esbuild therefore
 * needs SharedUi/node_modules to resolve Angular packages from the calling application.
 * Keeping one link per application avoids duplicate Angular package instances and makes
 * a fresh BackOffice checkout independent from Website.
 */
import { lstatSync, mkdirSync, readlinkSync, rmSync, symlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const sharedUiRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const linkType = process.platform === 'win32' ? 'junction' : 'dir';

function inspectLink(link, linkRoot) {
  try {
    const stats = lstatSync(link);

    if (!stats.isSymbolicLink()) {
      return { kind: 'directory' };
    }

    return { kind: 'link', target: resolve(linkRoot, readlinkSync(link)) };
  } catch {
    return { kind: 'missing' };
  }
}

export function linkSharedUi(applicationRoot = process.cwd(), sharedRoot = sharedUiRoot) {
  const resolvedSharedUiRoot = resolve(sharedRoot);
  const target = join(resolve(applicationRoot), 'node_modules');
  const link = join(resolvedSharedUiRoot, 'node_modules');
  const existing = inspectLink(link, resolvedSharedUiRoot);

  if (existing.kind === 'directory') {
    console.log(`[link-shared-ui] ${link} is a real directory, leaving it untouched.`);
    return;
  }

  if (existing.kind === 'link' && existing.target === target) {
    return;
  }

  if (existing.kind === 'link') {
    rmSync(link, { recursive: true, force: true });
  }

  mkdirSync(resolvedSharedUiRoot, { recursive: true });
  symlinkSync(target, link, linkType);
  console.log(`[link-shared-ui] linked ${link} -> ${target}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  linkSharedUi();
}
