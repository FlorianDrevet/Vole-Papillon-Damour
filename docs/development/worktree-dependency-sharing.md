# Sharing frontend dependencies between worktrees

Git worktrees have separate working directories, so each one normally needs its own `node_modules`. This helper reuses an already installed frontend dependency tree when another worktree has the exact same `package-lock.json`.

## Enable it once per clone

Run this from any worktree that contains the script:

```powershell
node tools/worktree-dependency-sharing.mjs --install
```

The installer copies the helper into the repository's shared Git hooks directory and adds a marked block to `post-checkout`. The hook directory belongs to the local clone and is shared by its linked worktrees. The installed helper and dependency files stay outside version control, and installation changes no tracked repository files. Existing hook content, including Graphify's hook, is preserved. Installation is idempotent.

After installation, `git worktree add` automatically checks for matching dependencies. Existing `node_modules` directories are left alone. To remove the local hook and helper, run:

```powershell
node tools/worktree-dependency-sharing.mjs --uninstall
```

The installer refuses to modify a custom `core.hooksPath`, since that hook directory may be shared with unrelated repositories.

## What gets reused

- Apps are discovered from `src/*/package-lock.json`.
- The lockfiles must have the same SHA-256 hash. The source install must also have a valid npm hidden lockfile at `node_modules/.package-lock.json`, with no unexpected or linked packages.
- Package files are hardlinked to the source worktree when the filesystem allows it. This lets all linked worktrees use the same file contents on disk. If hardlinks are unavailable, the helper copies those files and still completes the checkout.
- `.bin` launchers and `.package-lock.json` are copied into the new worktree. `.cache`, `.vite`, and `.angular` are omitted so each worktree can regenerate its own caches. Symlinks outside `.bin` are skipped to avoid carrying worktree-specific links across.
- The hook never runs `npm install` or `npm ci`. If no matching installed tree exists yet, install the dependencies once in that worktree; later worktrees with the same lockfile can reuse it.

Treat installed package files as disposable: editing a file that was hardlinked into multiple worktrees changes that file for all of them. Running `npm ci` after changing dependencies replaces the current worktree's `node_modules`; it does not refresh other worktrees.

## NuGet

NuGet already stores downloaded packages in a user-wide global package cache by default (`%USERPROFILE%\.nuget\packages` on Windows), shared by all worktrees on that machine. This helper does not change NuGet settings or share per-worktree build output such as `obj/`.
