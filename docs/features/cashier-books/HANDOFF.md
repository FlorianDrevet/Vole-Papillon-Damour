# Handoff Codex — Livres dans la caisse Android

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Réintroduire l'onglet **Livres** dans l'application Android de caisse (`src/MauiCashApp`), après
Buvette et Loto. La tablette affiche les livres disponibles avec leur prix, les ajoute au panier
et calcule le total. Elle n'enregistre **aucune** sortie ni vente : les sorties de livres se font
dans la nouvelle application (Scanette).

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-cashier-books`
- Branche : `feat/cashier-books`, qui contient déjà la conception (spec, maquettes, plan). La PR
  livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/cashier-books/01-fonctionnel.md` : **la spec, elle fait foi**
2. `docs/features/cashier-books/02-plan.md` : **le plan à exécuter** (11 tâches, de 0 à 10)
3. `docs/features/cashier-books/maquettes/README.md`, puis `maquettes/preview/index.html`
4. Skills du repo : `.github/skills/delivery-workflow/SKILL.md` et `.github/skills/tdd-workflow/SKILL.md`.
   Aucune skill spécifique à MAUI n'existe dans le repo.
5. Code réel : `src/MauiCashApp/ViewModels/MainViewModel.cs`, `src/MauiCashApp/MainPage.xaml`,
   `src/MauiCashApp/Constants/Categories.cs`, `src/MauiCashApp/Services/CashierProductFilter.cs`,
   `src/MauiCashApp/Services/ProductService.cs`, et `src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`
   (le projet de tests compile les fichiers de production **par lien** : tout nouveau fichier doit y
   être ajouté).

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 3 | `Main.html` | `E1-onglet-livres` |
| 4 | `Main.html` | `E2-grille-livres`, `E2-carte-livre`, `E2-prix-livre` |
| 5 | `Panier.html` | `E3-ligne-livre`, `E3-total` |
| 6 | `Etats.html` | `E4-etat-vide` |
| 7 | `Etats.html` | `E4-etat-chargement` |
| 8 | `Etats.html` | `E4-bandeau-hors-ligne` |

**Largeur de vérification : tablette paysage 1280 × 720.** Les maquettes sont à cette taille. Ne pas
vérifier à 390 ni 1440 px : l'application est une caisse tablette.

**Vérification visuelle** : la skill `playwright` ne convient pas, car l'application est une app MAUI
sans rendu web. Capturer l'écran sur émulateur Android ou tablette, le comparer aux pages `preview`
à 1280 × 720, et joindre les captures à la PR. Les écarts connus entre maquettes et code sont listés
en tête du plan : ils ne sont pas à corriger.

## Spécifique à cette feature

- **Constantes non négociables** :
  - le prix affiché est `Price` de la fiche produit, avec le format existant `{0:F2} €` de `ProductCard` ;
  - aucun calcul de remise, aucun arrondi nouveau ;
  - ne pas modifier `ProductCard` ni le calcul du nombre de colonnes dans `MainPage.xaml.cs`.
- **Interdits** :
  - aucune modification de l'API, du backend (`src/Backend`) ni de la base ;
  - aucun enregistrement de vente ou de sortie sur la tablette (règle CL-05) ;
  - ne pas publier l'APK et ne pas toucher aux liens de téléchargement du BackOffice (arbitrage A3 du plan) ;
  - ne pas modifier `01-fonctionnel.md`, `maquettes/` ni `02-plan.md` ;
  - ne pas ajouter de dépendance NuGet.
- **Flags / configuration** : aucun.
- **Azure / infra** : aucun. Pas de déploiement, pas de commande `az`.

## Suite de validation (surfaces touchées : MAUI uniquement)

Le skill mentionne les surfaces backend, front et infra : elles ne sont pas touchées ici. Lancer :

- `dotnet build src/MauiCashApp/ShopAppVpd.csproj --framework net10.0-android --configuration Release --no-restore`
- `dotnet test src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`
- le build APK de la tâche 10 (sans publication) ;
- `graphify update .` en fin de session.

La CI ne lance pas `MauiCashApp.Tests` : copier les sorties de `dotnet test` dans la section « Validation »
de la PR.

## PR

- Titre : `feat(cash): livres dans la caisse Android`
- « Risks and follow-up » :
  - écarts maquettes ↔ code : colonnes de la grille (calcul existant), taille des cartes, onglet
    sélectionné (arbitrage A2, pas de changement) ;
  - arbitrages A1 (portée du chargement et du bandeau hors ligne : onglet Livres seulement) et A3
    (APK non publié) : à confirmer par l'utilisateur ;
  - cache : les livres n'apparaissent hors ligne qu'après une première synchronisation réussie avec
    la nouvelle version, car `ProductDatabase.SaveProductsAsync` remplace le cache ;
  - données : les produits de section Livres existent déjà en base avec un prix (confirmé par
    l'utilisateur, Q-02) ;
  - documentation : dérogation à `RG-50` pour la calculette, mise à jour des docs de la bourse (tâche 9) ;
  - la CI ne lance pas les tests MAUI.
