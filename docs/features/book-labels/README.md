# Étiquettes des livres sans ISBN et impression

> Demande d'origine (2026-11-19) : « Il faut un écran pour ajouter les livres sans ISBN, y
> mettre les boutons d'impression et supporter le scan de ces codes qui ne sont pas des
> ISBN. L'objectif : rentrer toutes les infos du livre, que la plateforme génère un code
> identifiable comme un ISBN, que depuis la plateforme on imprime son code-barres sur
> l'étiqueteuse T50M Pro, et qu'en caisse on le scanne pour le sortir de la bourse. Même
> chose pour les livres rares. Sur l'écran de scan, un bouton pour réimprimer l'étiquette
> ISBN quand l'ISBN est peu lisible. La vraie connexion à l'étiqueteuse viendra plus tard :
> il faut tout préparer et simuler les appels. »

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-book-labels`
- Branche : `feat/book-labels` (aucune PR ouverte à ce stade)
- Canevas Claude Design : https://claude.ai/artifact/AEzgNYCDYr9KuxomHmk3Nx

## Statut

- [x] 1. Spécification fonctionnelle validée (2026-11-19) — [`01-fonctionnel.md`](01-fonctionnel.md)
- [x] 2. Maquettes exportées — [`maquettes/`](maquettes/README.md)
- [x] 3. Plan rédigé, `check_zones.py` vert — [`02-plan.md`](02-plan.md)
- [ ] 4. Handoff Codex — remplacé : implémentation faite directement par Claude, à la demande de l'utilisateur
- [ ] 5. PR — non ouverte (demande : commit et push sur la branche uniquement)

Implémentation : backend, scanette et site Catalog livrés sur `feat/book-labels` (voir le plan,
tâches T1 à T10). Restent hors périmètre de cette branche : la connexion réelle à l'étiqueteuse
(RG-178), la date de dernière impression (RG-177), et l'affichage dédié de la caisse (E8).
