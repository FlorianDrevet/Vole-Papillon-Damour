import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const helperFileName = 'vpd-worktree-dependency-sharing.mjs';
const hookStart = '# vpd-worktree-dependency-sharing-start';
const hookEnd = '# vpd-worktree-dependency-sharing-end';
const legacyHookStart = '# vpd-worktree-deps-start';
const legacyHookEnd = '# vpd-worktree-deps-end';
const cacheDirectories = new Set(['.cache', '.vite', '.angular']);

function removeMarkedBlock(content, startMarker, endMarker) {
  const lineEnding = content.includes('\r\n') ? '\r\n' : '\n';
  const lines = content.split(/\r?\n/);
  const kept = [];
  for (let index = 0; index < lines.length; index += 1) {
    if (lines[index].trim() !== startMarker) {
      kept.push(lines[index]);
      continue;
    }

    const endIndex = lines.findIndex((line, candidateIndex) =>
      candidateIndex > index && line.trim() === endMarker);
    if (endIndex === -1) {
      kept.push(lines[index]);
      continue;
    }
    index = endIndex;
  }

  return kept.join(lineEnding);
}

function managedHookBlock() {
  return [
    hookStart,
    'if [ "${3:-0}" = "1" ] && command -v node >/dev/null 2>&1; then',
    '  VPD_WORKTREE_HOOKS_DIR=$(git rev-parse --git-path hooks 2>/dev/null)',
    `  VPD_WORKTREE_DEPS_SCRIPT="$VPD_WORKTREE_HOOKS_DIR/${helperFileName}"`,
    '  if [ -n "$VPD_WORKTREE_HOOKS_DIR" ] && [ -f "$VPD_WORKTREE_DEPS_SCRIPT" ]; then',
    '    node "$VPD_WORKTREE_DEPS_SCRIPT" --checkout "$PWD" || echo "[vpd-worktree-deps] Sharing skipped; checkout continues."',
    '  fi',
    'fi',
    hookEnd,
  ].join('\n');
}

export function upsertPostCheckoutHook(existingContent) {
  const content = removeManagedPostCheckoutHook(existingContent);

  const lineEnding = content.includes('\r\n') ? '\r\n' : '\n';
  const lines = content.split(/\r?\n/);
  const hadTrailingLineEnding = content.endsWith('\n');
  if (hadTrailingLineEnding) lines.pop();

  if (lines.length === 0 || !lines[0].startsWith('#!')) {
    lines.unshift('#!/bin/sh');
  }
  lines.splice(1, 0, ...managedHookBlock().split('\n'));

  return `${lines.join(lineEnding)}${lineEnding}`;
}

export function removeManagedPostCheckoutHook(existingContent) {
  const withoutCurrentHook = removeMarkedBlock(existingContent, hookStart, hookEnd);
  return removeMarkedBlock(withoutCurrentHook, legacyHookStart, legacyHookEnd);
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function isRealDirectory(directoryPath) {
  try {
    const stats = fs.lstatSync(directoryPath);
    return stats.isDirectory() && !stats.isSymbolicLink();
  } catch {
    return false;
  }
}

export function installedTreeMatchesLock(lockPath, modulesPath) {
  try {
    if (!isRealDirectory(modulesPath)) return false;

    const expectedPackages = readJson(lockPath).packages ?? {};
    const installedPackages = readJson(path.join(modulesPath, '.package-lock.json')).packages ?? {};
    const installedEntries = Object.entries(installedPackages).filter(([name]) => name !== '');
    if (installedEntries.length === 0) return false;

    for (const [name, installed] of installedEntries) {
      const expected = expectedPackages[name];
      if (!expected || installed.link === true) return false;

      for (const field of ['version', 'resolved', 'integrity']) {
        if (expected[field] !== undefined && installed[field] !== undefined && expected[field] !== installed[field]) {
          return false;
        }
      }
    }

    return true;
  } catch {
    return false;
  }
}

function isExcluded(relativePath) {
  const segments = relativePath.split(path.sep);
  return segments.some((segment) => cacheDirectories.has(segment));
}

function isWithin(directoryPath, candidatePath) {
  const relative = path.relative(directoryPath, candidatePath);
  return relative === '' || (!relative.startsWith(`..${path.sep}`) && relative !== '..' && !path.isAbsolute(relative));
}

export async function cloneNodeModules(sourcePath, targetPath) {
  const result = { hardlinks: 0, copiedFiles: 0, skippedLinks: 0 };
  const fileOperations = [];
  await fs.promises.mkdir(targetPath, { recursive: false });

  async function collectFiles(sourceDirectory, targetDirectory, relative = '') {
    const entries = await fs.promises.readdir(sourceDirectory, { withFileTypes: true });

    for (const entry of entries) {
      const childRelative = relative ? path.join(relative, entry.name) : entry.name;
      if (isExcluded(childRelative)) continue;

      const source = path.join(sourceDirectory, entry.name);
      const target = path.join(targetDirectory, entry.name);
      const insideBin = childRelative.split(path.sep).includes('.bin');

      if (entry.isSymbolicLink()) {
        if (!insideBin) {
          result.skippedLinks += 1;
          continue;
        }

        const linkTarget = await fs.promises.readlink(source);
        if (path.isAbsolute(linkTarget) || !isWithin(sourcePath, path.resolve(path.dirname(source), linkTarget))) {
          result.skippedLinks += 1;
          continue;
        }

        fileOperations.push({ kind: 'symlink', source, target, linkTarget });
        continue;
      }

      if (entry.isDirectory()) {
        await fs.promises.mkdir(target, { recursive: true });
        await collectFiles(source, target, childRelative);
        continue;
      }
      if (!entry.isFile()) continue;

      const kind = insideBin || childRelative === '.package-lock.json' ? 'copy' : 'link';
      fileOperations.push({ kind, source, target });
    }
  }

  try {
    await collectFiles(sourcePath, targetPath);

    let nextOperation = 0;
    async function processFiles() {
      while (nextOperation < fileOperations.length) {
        const operation = fileOperations[nextOperation++];
        if (operation.kind === 'copy') {
          await fs.promises.copyFile(operation.source, operation.target);
          result.copiedFiles += 1;
          continue;
        }
        if (operation.kind === 'symlink') {
          try {
            const targetStats = await fs.promises.stat(operation.source);
            await fs.promises.symlink(operation.linkTarget, operation.target, targetStats.isDirectory() ? 'junction' : 'file');
          } catch {
            result.skippedLinks += 1;
          }
          continue;
        }

        try {
          await fs.promises.link(operation.source, operation.target);
          result.hardlinks += 1;
        } catch {
          await fs.promises.copyFile(operation.source, operation.target);
          result.copiedFiles += 1;
        }
      }
    }

    const workerCount = Math.min(64, fileOperations.length);
    const workers = await Promise.allSettled(Array.from({ length: workerCount }, () => processFiles()));
    const failure = workers.find((worker) => worker.status === 'rejected');
    if (failure) throw failure.reason;

    return result;
  } catch (error) {
    await fs.promises.rm(targetPath, { recursive: true, force: true });
    throw error;
  }
}

function hashFile(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex');
}

function isPathPresent(targetPath) {
  try {
    fs.lstatSync(targetPath);
    return true;
  } catch {
    return false;
  }
}

export async function shareWorktreeDependencies(currentRoot, worktreeRoots, log = console.log) {
  const root = path.resolve(currentRoot);
  const sourceDirectory = path.join(root, 'src');
  if (!fs.existsSync(sourceDirectory)) return [];

  const uniqueRoots = [...new Set(worktreeRoots.map((candidate) => path.resolve(candidate)))];
  const apps = fs.readdirSync(sourceDirectory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((app) => fs.existsSync(path.join(sourceDirectory, app, 'package-lock.json')));
  const results = [];

  for (const app of apps) {
    const appRoot = path.join(sourceDirectory, app);
    const lockPath = path.join(appRoot, 'package-lock.json');
    const modulesPath = path.join(appRoot, 'node_modules');
    if (isPathPresent(modulesPath)) continue;

    const wantedHash = hashFile(lockPath);
    let sourceModulesPath;

    for (const candidateRoot of uniqueRoots) {
      if (candidateRoot.toLowerCase() === root.toLowerCase()) continue;

      const candidateApp = path.join(candidateRoot, 'src', app);
      const candidateLock = path.join(candidateApp, 'package-lock.json');
      const candidateModules = path.join(candidateApp, 'node_modules');
      if (!fs.existsSync(candidateLock) || !isRealDirectory(candidateModules)) continue;
      if (hashFile(candidateLock) !== wantedHash) continue;
      if (!installedTreeMatchesLock(candidateLock, candidateModules)) continue;

      sourceModulesPath = candidateModules;
      break;
    }

    if (!sourceModulesPath) {
      log(`[vpd-worktree-deps] ${app}: no installed tree matches this lockfile yet; run npm ci once in this worktree.`);
      continue;
    }

    try {
      const clone = await cloneNodeModules(sourceModulesPath, modulesPath);
      results.push({ app, sourceModulesPath, ...clone });
    } catch (error) {
      log(`[vpd-worktree-deps] ${app}: could not reuse installed files (${error.message}); checkout continues.`);
    }
  }

  if (results.length > 0) {
    const details = results.map(({ app, hardlinks, copiedFiles, skippedLinks }) =>
      `${app}: ${hardlinks.toLocaleString()} hardlinked files, ${copiedFiles.toLocaleString()} local files, ${skippedLinks.toLocaleString()} skipped links`);
    log(`[vpd-worktree-deps] Reused dependencies: ${details.join(' | ')}.`);
  }

  return results;
}

function runGit(args, cwd) {
  return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}

function getRepositoryRoot(cwd) {
  return runGit(['rev-parse', '--show-toplevel'], cwd);
}

function assertDefaultHooksPath(repoRoot) {
  try {
    const configured = runGit(['config', '--show-origin', '--get-regexp', '^core\\.hooksPath$'], repoRoot);
    if (configured) {
      throw new Error('core.hooksPath is configured. To avoid changing hooks shared with other repositories, install this tool only when Git uses its default hooks directory.');
    }
  } catch (error) {
    if (error.message.startsWith('core.hooksPath is configured.')) throw error;
    if (error.status !== 1) throw error;
  }
}

export function installWorktreeDependencyHook(cwd = process.cwd()) {
  const repoRoot = getRepositoryRoot(cwd);
  assertDefaultHooksPath(repoRoot);

  const hookDirectory = runGit(['rev-parse', '--git-path', 'hooks'], repoRoot);
  const absoluteHookDirectory = path.resolve(repoRoot, hookDirectory);
  const helperTarget = path.join(absoluteHookDirectory, helperFileName);
  const helperSource = fileURLToPath(import.meta.url);
  const hookPath = path.join(absoluteHookDirectory, 'post-checkout');

  fs.mkdirSync(absoluteHookDirectory, { recursive: true });
  if (path.resolve(helperSource).toLowerCase() !== path.resolve(helperTarget).toLowerCase()) {
    fs.copyFileSync(helperSource, helperTarget);
  }

  const existing = fs.existsSync(hookPath) ? fs.readFileSync(hookPath, 'utf8') : '';
  const mode = fs.existsSync(hookPath) ? fs.statSync(hookPath).mode & 0o777 : 0o755;
  fs.writeFileSync(hookPath, upsertPostCheckoutHook(existing), 'utf8');
  fs.chmodSync(hookPath, mode | 0o111);

  return { repoRoot, hookPath, helperPath: helperTarget };
}

export function uninstallWorktreeDependencyHook(cwd = process.cwd()) {
  const repoRoot = getRepositoryRoot(cwd);
  assertDefaultHooksPath(repoRoot);

  const hookDirectory = runGit(['rev-parse', '--git-path', 'hooks'], repoRoot);
  const absoluteHookDirectory = path.resolve(repoRoot, hookDirectory);
  const hookPath = path.join(absoluteHookDirectory, 'post-checkout');
  const helperPath = path.join(absoluteHookDirectory, helperFileName);

  if (fs.existsSync(hookPath)) {
    const original = fs.readFileSync(hookPath, 'utf8');
    const updated = removeManagedPostCheckoutHook(original);
    if (updated !== original) {
      const mode = fs.statSync(hookPath).mode & 0o777;
      fs.writeFileSync(hookPath, updated, 'utf8');
      fs.chmodSync(hookPath, mode);
    }
  }
  fs.rmSync(helperPath, { force: true });

  return { repoRoot, hookPath, helperPath };
}

function worktreeRoots(repoRoot) {
  return runGit(['worktree', 'list', '--porcelain'], repoRoot)
    .split(/\r?\n/)
    .filter((line) => line.startsWith('worktree '))
    .map((line) => path.resolve(line.slice('worktree '.length)))
    .filter((worktree) => fs.existsSync(worktree));
}

async function main() {
  const [mode, rootArgument] = process.argv.slice(2);
  if (mode === '--install') {
    const installation = installWorktreeDependencyHook(rootArgument ?? process.cwd());
    console.log(`[vpd-worktree-deps] Installed local post-checkout helper at ${installation.hookPath}.`);
    console.log('[vpd-worktree-deps] This setup is local to this clone; no repository files were changed.');
    return;
  }
  if (mode === '--uninstall') {
    const uninstallation = uninstallWorktreeDependencyHook(rootArgument ?? process.cwd());
    console.log(`[vpd-worktree-deps] Removed the local dependency hook from ${uninstallation.hookPath}.`);
    return;
  }
  if (mode === '--checkout') {
    const root = getRepositoryRoot(rootArgument ?? process.cwd());
    await shareWorktreeDependencies(root, worktreeRoots(root));
    return;
  }

  console.error('Usage: node tools/worktree-dependency-sharing.mjs --install | --uninstall | --checkout [worktree-root]');
  process.exitCode = 2;
}

if (process.argv[1] && path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1])) {
  main().catch((error) => {
    console.error(`[vpd-worktree-deps] ${error.message}`);
    process.exitCode = 1;
  });
}
