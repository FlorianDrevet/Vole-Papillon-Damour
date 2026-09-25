# 09 — Prompt d'implémentation pour Codex (Luna)

> Prompt remis le 24 septembre 2026. Le copier tel quel.

````text
Tu implémentes la fonctionnalité « recommandations de livres » du catalogue
Vole-Papillon-Damour : une section « Dans le même esprit » sous chaque fiche (tout public),
et une section « Pour vous » (accueil + Mes achats) pour les membres connectés, fondée sur
leurs achats associés et désactivable. La conception est terminée et validée : tu exécutes
un plan, tu ne rediscutes pas l'architecture.

## Espace de travail — déjà créé, ne pas en créer d'autre

- Worktree : C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-book-recommendations
- Branche  : feat/book-recommendations
  Elle part de la branche de conception docs/book-recommendations-design (elle-même
  basée sur origin/main du 24/09/2026). Elle contient donc déjà la spécification, le plan,
  les maquettes et le benchmark. C'est volontaire : la PR finale livrera conception et
  implémentation ensemble.

## À lire avant d'écrire une ligne, dans cet ordre

1. AGENTS.md, puis .github/skills/delivery-workflow/SKILL.md, .github/skills/tdd-workflow/SKILL.md,
   .github/skills/cqrs-feature/SKILL.md, .github/skills/dotnet-patterns/SKILL.md,
   .github/skills/angular-patterns/SKILL.md, .github/skills/ui-ux-front-saas/SKILL.md
2. docs/recommandations-livres/07-specification.md — LA SPEC, elle fait foi
3. docs/recommandations-livres/08-plan-implementation.md — LE PLAN À EXÉCUTER (14 tâches)
4. docs/recommandations-livres/05-benchmark.md §7 — pourquoi la méthode est celle-là
5. tools/recommendation-benchmark/bench/features.py et methods.py — la méthode de référence
   que la tâche 2 traduit en C#
6. docs/recommandations-livres/maquettes/index.html — les écrans et leurs zones

## Maquettes

Les écrans sont dans docs/recommandations-livres/maquettes/html/*.html (pages autonomes,
à ouvrir dans un navigateur). Chaque zone à implémenter porte un attribut data-zone
(ex. data-zone="R1-carte") ; chaque tâche UI du plan cite la page et ses zones :
- R1 Main.html, R2 FicheMobile.html, R3 FicheEtats.html → tâche 11
- R4 AccueilPourVous.html, R5 AccueilPourVousMobile.html, R8-chargement → tâche 12
- R6 MesAchatsPourVous.html, R7 ComptePreferences.html, R8-sans-achat, R8-desactive → tâche 13
Reprends les textes mot pour mot, les couleurs, typographies et espacements de ces pages,
en t'appuyant sur les composants existants (app-book-card variante home, panneaux du
compte). Vérifie chaque écran à 390 px et 1440 px : aucune barre de défilement
horizontale de page, seules les rangées de cartes défilent en mobile.

## Règles d'exécution

- Exécute les tâches 0 à 14 dans l'ordre, étape par étape, en cochant mentalement chaque
  case. Un commit par tâche, avec le message du plan (préfixe rtk pour git).
- Tests d'abord : écris le test, vérifie qu'il échoue pour la bonne raison, puis
  implémente. Le code de la tâche 2 a été compilé et testé tel quel (29 tests) : recopie-le
  exactement.
- Constantes non négociables : bonus série 0.20, tome suivant 0.10, même auteur 0.08,
  même forme 0.03, pénalité publics opposés 0.10, 30 voisins, 512 dimensions ; seuils
  d'affichage de la spec §6.
- Interdits : base graphe, Cosmos DB, type SQL VECTOR, ISBNdb, nouvelle ressource Azure
  (hors le déploiement d'embedding décrit en tâche 8), toute donnée personnelle dans un
  texte envoyé à Azure OpenAI, modification de l'agrégat Book ou du flux
  d'enrichissement existant.
- Nouveaux DbSet de IProjectDbContext : avec l'implémentation par défaut
  `=> throw new NotSupportedException();` (sinon les DbContext de test cassent).
- Le déploiement Azure `text-embedding-3-small` existe déjà (créé à la main) : le Bicep de
  la tâche 8 doit reprendre exactement ce nom, le SKU GlobalStandard et la capacité 50.
  Ne lance AUCUN déploiement Azure, AUCUN `az` en écriture : tu modifies le Bicep et tu le
  compiles (`az bicep build`), c'est tout.
- Tout reste désactivé par défaut (Recommendations:Enabled = false).
- Si un point du plan contredit le code réel (nom de fichier, signature, ligne), suis le
  code réel en restant fidèle à l'intention du plan, et note l'écart pour la PR. Si une
  contradiction touche la spec (règle métier, RGPD), arrête-toi et signale-la au lieu de
  trancher.
- Ne corrige pas les tests déjà en échec avant ton travail (tâche 0) : signale-les.

## Livraison (tâche 14 + delivery-workflow)

1. Suite complète verte : dotnet build + dotnet test (slnx), ng test + ng build (src/Catalog),
   az bicep build --file infra/main.bicep, puis graphify update .
2. rtk git fetch origin main ; rtk git rebase origin/main ; relance toute la suite.
3. rtk git push -u origin feat/book-recommendations
4. PR vers main, titre : « feat(catalog): book recommendations (similar books and personal suggestions) ».
   Corps au format du delivery-workflow. « Validation » : sorties exactes des commandes,
   captures ou description de la vérification 390/1440 px de chaque écran R1-R8.
   « Risks and follow-up » : les 4 points de la tâche 14, étape 3, plus tout écart au plan.
5. Ne merge pas. Rends : lien de la PR, chemin du worktree, branche, résultat des tests,
   liste des écarts au plan.
````
