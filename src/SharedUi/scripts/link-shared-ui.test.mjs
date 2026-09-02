import assert from 'node:assert/strict';
import { existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { tmpdir } from 'node:os';
import { linkSharedUi } from './link-shared-ui.mjs';

const scriptsRoot = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptsRoot, '../../..');

test('the SharedUi linker lives in the shared scripts directory', () => {
  assert.equal(existsSync(join(scriptsRoot, 'link-shared-ui.mjs')), true);
  assert.equal(existsSync(join(repositoryRoot, 'src/Website/scripts/link-shared-ui.mjs')), false);
});

test('both Angular applications invoke the shared linker', () => {
  const websitePackage = JSON.parse(
    readFileSync(join(repositoryRoot, 'src/Website/package.json'), 'utf8'),
  );
  const backOfficePackage = JSON.parse(
    readFileSync(join(repositoryRoot, 'src/BackOffice/package.json'), 'utf8'),
  );

  assert.equal(websitePackage.scripts['link:shared-ui'], 'node ../SharedUi/scripts/link-shared-ui.mjs');
  assert.equal(websitePackage.scripts.prestart, 'npm run link:shared-ui');
  assert.equal(websitePackage.scripts.prebuild, 'npm run link:shared-ui');
  assert.equal(backOfficePackage.scripts['link:shared-ui'], 'node ../SharedUi/scripts/link-shared-ui.mjs');
  assert.equal(backOfficePackage.scripts.prestart, 'npm run link:shared-ui');
  assert.equal(backOfficePackage.scripts.prebuild, 'npm run link:shared-ui');
});

test('the linker targets the calling application installation', () => {
  const testRoot = join(tmpdir(), `vpd-shared-ui-${process.pid}`);
  const applicationRoot = join(testRoot, 'BackOffice');
  const sharedRoot = join(testRoot, 'SharedUi');
  const applicationNodeModules = join(applicationRoot, 'node_modules');
  const sharedNodeModules = join(sharedRoot, 'node_modules');

  mkdirSync(applicationNodeModules, { recursive: true });

  try {
    linkSharedUi(applicationRoot, sharedRoot);

    assert.equal(lstatSync(sharedNodeModules).isSymbolicLink(), true);
    assert.equal(realpathSync(sharedNodeModules), realpathSync(applicationNodeModules));
  } finally {
    rmSync(testRoot, { recursive: true, force: true });
  }
});
