## graphify

This project has a Graphify knowledge graph at `graphify-out/`.

Rules:
- Before answering architecture or codebase questions, read `graphify-out/GRAPH_REPORT.md` for god nodes and community structure.
- If `graphify-out/wiki/index.md` exists, navigate it instead of reading raw files.
- If the Graphify MCP server is active, use its graph tools for structural exploration.
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost).

## Répartition Claude / Codex

- Claude **conçoit**, Codex (Luna, effort max) **implémente**. Toute demande de feature, d'évolution ou de correctif avec des écrans passe par la skill `/feature` (`.claude/skills/feature/SKILL.md`), même si l'utilisateur ne la nomme pas : worktree `../Vole-Papillon-Damour-<slug>` sur `feat/<slug>` depuis `origin/main`, spec fonctionnelle `.md`, maquettes Claude Design, plan, puis prompt Codex.
- Une feature = un dossier `docs/features/<slug>/` (spec, maquettes exportées avec `data-zone`, plan, `HANDOFF.md`) dans la branche que Codex reprend. Codex lit les maquettes depuis `maquettes/preview/`, jamais depuis un lien claude.ai.
- Claude n'écrit pas le code de production d'une feature passée par `/feature` et n'ouvre pas sa PR : Codex le fait avec `.agents/skills/implement-handoff/SKILL.md`.
- Langue : français pour les échanges et les documents de conception.

## Performance

- Avant de concevoir ou de relire un écran de liste/tableau/recherche, un endpoint de liste, une requête EF, un import/export, des images ou un appel sortant, lis `.github/skills/perf-guardrails/SKILL.md`.
- Ses règles §A sont **absolues** (pagination, filtre, tri et recherche toujours côté serveur, etc.), sans demander.
- Ses recommandations §B (Redis, CDN, palier SQL, réplicas…) ont un coût : propose-les en **une ligne** quand un déclencheur existe, n'implémente rien sans « oui », et consigne la réponse dans `docs/development/perf-decisions.md`.
