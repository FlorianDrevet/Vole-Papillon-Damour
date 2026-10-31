import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import {
  cloneNodeModules,
  installedTreeMatchesLock,
  removeManagedPostCheckoutHook,
  shareWorktreeDependencies,
  upsertPostCheckoutHook,
} from './worktree-dependency-sharing.mjs';

const packageEntry = {
  version: '1.0.0',
  resolved: 'https://registry.npmjs.org/example/-/example-1.0.0.tgz',
  integrity: 'sha512-example',
};

function writeJson(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, JSON.stringify(value, null, 2));
}

function makeInstalledTree(root, appName, lock = { packages: { 'node_modules/example': packageEntry } }) {
  const appRoot = path.join(root, 'src', appName);
  const modulesPath = path.join(appRoot, 'node_modules');
  fs.mkdirSync(path.join(modulesPath, 'example'), { recursive: true });
  writeJson(path.join(appRoot, 'package-lock.json'), lock);
  writeJson(path.join(modulesPath, '.package-lock.json'), {
    lockfileVersion: 3,
    packages: { 'node_modules/example': packageEntry },
  });
  fs.writeFileSync(path.join(modulesPath, 'example', 'index.js'), 'module.exports = 1;\n');
  fs.mkdirSync(path.join(modulesPath, '.bin'), { recursive: true });
  fs.writeFileSync(path.join(modulesPath, '.bin', 'example.cmd'), 'node example.js\r\n');
  fs.mkdirSync(path.join(modulesPath, 'example', '.cache'), { recursive: true });
  fs.writeFileSync(path.join(modulesPath, 'example', '.cache', 'generated.json'), '{}');
  return { appRoot, modulesPath };
}

test('upsertPostCheckoutHook_PreservesExistingHookAndAddsOneIdempotentManagedBlock', () => {
  const existing = [
    '#!/bin/sh',
    '# vpd-worktree-deps-start',
    'old local helper call',
    '# vpd-worktree-deps-end',
    '# graphify-checkout-hook-start',
    'echo graphify',
    '# graphify-checkout-hook-end',
    '',
  ].join('\n');

  const updated = upsertPostCheckoutHook(existing);
  const markerCount = (updated.match(/vpd-worktree-dependency-sharing-start/g) ?? []).length;

  assert.match(updated, /^#!\/bin\/sh\n/);
  assert.match(updated, /# graphify-checkout-hook-start/);
  assert.doesNotMatch(updated, /old local helper call/);
  assert.equal(markerCount, 1);
  assert.equal(upsertPostCheckoutHook(updated), updated);
  const removed = removeManagedPostCheckoutHook(updated);
  assert.doesNotMatch(removed, /vpd-worktree-dependency-sharing-start/);
  assert.match(removed, /# graphify-checkout-hook-start/);

  const incompleteHook = '#!/bin/sh\n# vpd-worktree-dependency-sharing-start\necho preserve this hook\n';
  assert.equal(removeManagedPostCheckoutHook(incompleteHook), incompleteHook);
});

test('installedTreeMatchesLock_RejectsExtraOrLinkedPackagesAndAcceptsOmittedOptionalPackages', (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vpd-deps-lock-'));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));

  const lockPath = path.join(root, 'package-lock.json');
  const modulesPath = path.join(root, 'node_modules');
  fs.mkdirSync(modulesPath);
  writeJson(lockPath, {
    packages: {
      'node_modules/example': packageEntry,
      'node_modules/optional-example': { version: '2.0.0' },
    },
  });
  const hiddenLockPath = path.join(modulesPath, '.package-lock.json');
  writeJson(hiddenLockPath, { packages: { 'node_modules/example': packageEntry } });

  assert.equal(installedTreeMatchesLock(lockPath, modulesPath), true);
  writeJson(hiddenLockPath, {
    packages: {
      'node_modules/example': packageEntry,
      'node_modules/extra': { version: '1.0.0' },
    },
  });
  assert.equal(installedTreeMatchesLock(lockPath, modulesPath), false);
  writeJson(hiddenLockPath, { packages: { 'node_modules/example': { ...packageEntry, link: true } } });
  assert.equal(installedTreeMatchesLock(lockPath, modulesPath), false);
});

test('shareWorktreeDependencies_ClonesOnlyAnInstalledTreeWithTheSameLockfile', async (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vpd-deps-sharing-'));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const sourceRoot = path.join(root, 'source');
  const targetRoot = path.join(root, 'target');
  const lock = { lockfileVersion: 3, packages: { 'node_modules/example': packageEntry } };
  const source = makeInstalledTree(sourceRoot, 'Website', lock);
  const targetApp = path.join(targetRoot, 'src', 'Website');
  fs.mkdirSync(targetApp, { recursive: true });
  writeJson(path.join(targetApp, 'package-lock.json'), lock);

  const logs = [];
  const results = await shareWorktreeDependencies(targetRoot, [sourceRoot, targetRoot], (message) => logs.push(message));
  const targetModules = path.join(targetApp, 'node_modules');

  assert.equal(fs.readFileSync(path.join(targetModules, 'example', 'index.js'), 'utf8'), 'module.exports = 1;\n');
  assert.equal(fs.readFileSync(path.join(targetModules, '.bin', 'example.cmd'), 'utf8'), 'node example.js\r\n');
  assert.equal(fs.existsSync(path.join(targetModules, 'example', '.cache')), false);
  assert.notEqual(fs.statSync(path.join(source.modulesPath, '.package-lock.json')).ino,
    fs.statSync(path.join(targetModules, '.package-lock.json')).ino);
  assert.equal(fs.statSync(path.join(source.modulesPath, 'example', 'index.js')).ino,
    fs.statSync(path.join(targetModules, 'example', 'index.js')).ino);
  assert.equal(results.length, 1);
  assert.match(logs.join('\n'), /Reused dependencies/);

  const mismatchRoot = path.join(root, 'mismatch');
  const mismatchApp = path.join(mismatchRoot, 'src', 'Website');
  fs.mkdirSync(mismatchApp, { recursive: true });
  writeJson(path.join(mismatchApp, 'package-lock.json'), { packages: { 'node_modules/other': { version: '9.0.0' } } });
  const mismatchResults = await shareWorktreeDependencies(mismatchRoot, [sourceRoot, mismatchRoot], () => {});
  assert.equal(mismatchResults.length, 0);
  assert.equal(fs.existsSync(path.join(mismatchApp, 'node_modules')), false);
});

test('cloneNodeModules_CopiesLocalLaunchersAndMetadataWhileHardlinkingPackageFiles', async (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vpd-deps-clone-'));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const source = path.join(root, 'source');
  const target = path.join(root, 'target');
  fs.mkdirSync(path.join(source, 'package'), { recursive: true });
  fs.mkdirSync(path.join(source, '.bin'), { recursive: true });
  fs.writeFileSync(path.join(source, 'package', 'index.js'), 'exports.answer = 42;\n');
  fs.writeFileSync(path.join(source, '.bin', 'tool.cmd'), 'node tool.js\r\n');
  fs.writeFileSync(path.join(source, '.package-lock.json'), '{}');
  fs.mkdirSync(path.join(source, '.vite'), { recursive: true });
  fs.writeFileSync(path.join(source, '.vite', 'cache'), 'ignore');

  const result = await cloneNodeModules(source, target);

  assert.equal(fs.readFileSync(path.join(target, '.bin', 'tool.cmd'), 'utf8'), 'node tool.js\r\n');
  assert.equal(fs.existsSync(path.join(target, '.package-lock.json')), true);
  assert.equal(fs.existsSync(path.join(target, '.vite')), false);
  assert.equal(fs.statSync(path.join(source, 'package', 'index.js')).ino,
    fs.statSync(path.join(target, 'package', 'index.js')).ino);
  assert.ok(result.hardlinks >= 1);
  assert.ok(result.copiedFiles >= 2);
});
