# Worktree dependency sharing

- `tools/worktree-dependency-sharing.mjs --install` installs a clone-local `post-checkout` hook and copies the helper into the shared Git hooks directory. The hook runs after branch checkouts, including `git worktree add`.
- The tool discovers frontend apps under `src/*` with `package-lock.json`. It reuses an existing `node_modules` only when the lockfile bytes match exactly and npm's hidden lock contains only packages from that lockfile.
- Regular package files are hardlinked when possible; `.bin` launchers and `.package-lock.json` are copied. `.cache`, `.vite`, and `.angular` folders are omitted. Symlinks outside `.bin` are skipped to avoid leaking worktree-specific links.
- Existing `node_modules` directories are not changed. A worktree with a new lockfile needs one normal `npm ci`; subsequent worktrees with that lock can reuse it. Hardlinks require the source and target to be on a filesystem that supports them; otherwise the helper copies files.
- `--uninstall` removes only the marked hook block and copied helper, preserving other `post-checkout` content. The installer refuses custom `core.hooksPath` settings to avoid changing a hook directory shared with unrelated repositories.
- NuGet's default global package cache is already user-wide and shared between worktrees; this tool does not change it or share per-worktree `obj/` build output.
