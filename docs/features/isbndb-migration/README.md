# ISBNdb, source principale des fiches livres

> Demande d'origine (2026-10-23) : « Je souhaite passer sur l'API isbndb.com […] en faire mon
> API principale avec un fallback vers les autres API utilisées, mais qui ne soit que
> temporaire. »

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-isbndb-migration`
- Branche : `feat/isbndb-migration`
- Canvas Claude Design : **pas de maquettes**. Une ébauche a été abandonnée le 2026-10-23 à la demande de l'utilisateur, car elle ne correspondait pas aux visuels actuels. Codex dessine les écrans E1 à E5 en reprenant les composants, variables CSS et libellés des écrans existants les plus proches. `check_zones.py` ne s'applique donc pas (aucune zone `data-zone`).

## Statut

- [x] 1. Spécification fonctionnelle validée — [`01-fonctionnel.md`](01-fonctionnel.md)
- [x] 2. Maquettes — sans objet : Codex s'aligne sur les écrans existants (voir ci-dessus)
- [x] 3. Plan validé (`check_zones.py` sans objet) — [`02-plan.md`](02-plan.md)
- [x] 4. Handoff Codex remis — [`HANDOFF.md`](HANDOFF.md)
- [x] 5. PR ouverte par Codex — [PR #340](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/340)
