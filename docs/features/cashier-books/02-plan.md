# Livres dans la caisse Android — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

## Règle de priorité

En cas de divergence : **spec > plan > maquette**. Les divergences connues sont listées
ci-dessous : ne pas les « corriger » en suivant la maquette.

## Divergences connues entre maquettes et code

- **Nombre de colonnes de la grille** : `MainPage.xaml.cs` (`OnSizeAllocated`) calcule le
  nombre de colonnes à partir de la largeur. La maquette montre 4 colonnes à titre indicatif.
  Ne pas toucher à ce calcul.
- **Taille des cartes** : `Views/ProductCard.xaml` fait 140 × 175 px. La maquette est plus
  haute. Garder `ProductCard` tel quel.
- **Onglet sélectionné** : la maquette montre un fond blanc sur l'onglet actif. Le code
  actuel n'a aucun état sélectionné. Voir arbitrage A2.
- **Chargement et hors ligne** : n'existent pas dans le code aujourd'hui. Ils sont ajoutés
  aux tâches 7 et 8.

## Arbitrages à confirmer (valeurs par défaut appliquées)

- **A1 — Portée du chargement et du bandeau hors ligne.** Défaut : limités à l'onglet Livres,
  comme l'état E4 de la spec. Alternative : toute la caisse. Modifier les conditions de visibilité
  des tâches 7 et 8 si l'alternative est retenue.
- **A2 — Onglet sélectionné.** Défaut : aucun changement visuel pour les trois onglets (la spec
  dit « même forme que les autres »). La maquette montre un état actif : à ajouter seulement si
  l'utilisateur le demande.
- **A3 — Publication de l'APK.** Défaut : hors de cette PR. Le README de `src/MauiCashApp`
  décrit la copie de l'APK vers le BackOffice. La décision de publier une version revient à
  l'utilisateur.

## Tâche 0 — État initial (avant toute modification)

- Restaurer et compiler l'application Android :
  `dotnet restore src/MauiCashApp/ShopAppVpd.csproj --property:TargetFramework=net10.0-android`
  puis `dotnet build src/MauiCashApp/ShopAppVpd.csproj --framework net10.0-android --configuration Release --no-restore`.
- Lancer les tests de la caisse : `dotnet test src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`.
- Noter les échecs déjà présents sans les corriger.
- **Piège** : la CI ne lance pas `MauiCashApp.Tests` (seuls les tests Backend sont lancés dans
  `.github/workflows/ci.yml`). Les tests de la caisse se lancent donc à la main.
- **Piège** : le projet de tests compile les fichiers de production par lien
  (`src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`, éléments `Compile Include="..\MauiCashApp\..."`).
  Tout nouveau fichier de production utilisé par `MainViewModel`, `CashierProductFilter` ou
  `IProductService` doit être ajouté à cette liste avec un `Link` sous `Production\`.

## Tâche 1 — Tests d'abord (rouge)

Fichiers : `src/MauiCashApp.Tests/CashierProductFilterTests.cs`, `src/MauiCashApp.Tests/MainViewModelTests.cs`.

- `FromApi` et `FromCache` : un livre disponible (`ProductSection.Book`) est inclus ; un livre
  désactivé est exclu. Les cas buvette et loto existants restent.
- Remplacer `Categories_OnlyExposeBuvetteAndLoto` par `Categories_ExposeBuvetteLotoEtLivres`,
  qui attend `[ProductSection.Bar, ProductSection.Bingo, ProductSection.Book]`.
- `MainViewModelTests` : sélectionner la catégorie Livres ne laisse dans `FilteredProducts`
  que les livres disponibles.
- Les tests doivent échouer avant la tâche 2.
- Message de commit : `test(cash): livres dans le filtre et les catégories de la caisse`.

## Tâche 2 — Filtre des sections

Fichier : `src/MauiCashApp/Services/CashierProductFilter.cs`.

- `IsCashierSection` accepte `nameof(ProductSection.Book)` en plus de `Bar` et `Bingo`.
- Les tests de la tâche 1 passent.
- Message de commit : `feat(cash): inclure les livres dans le filtre de caisse`.

## Tâche 3 — Catégorie Livres (E1)

Fichier : `src/MauiCashApp/Constants/Categories.cs`.

- Ajouter `Livres = new() { Name = "Livres", Icon = "book.png", Section = ProductSection.Book }`.
- `All` = Buvette, Loto, Livres, dans cet ordre.
- Ne pas modifier `MainPage.xaml` pour la barre d'onglets : elle est générée depuis
  `Categories` (`BindableLayout.ItemsSource="{Binding Categories}"`).
- L'icône `book.png` est générée à la compilation depuis `Resources/Images/book.svg`
  (`MauiImage Include="Resources\Images\*"`), comme `drink.png` et `loto.png`.
- **Maquette :** preview/Main.html — zones E1-onglet-livres
- Message de commit : `feat(cash): onglet Livres`.

## Tâche 4 — Grille de livres (E2)

Aucun changement de code attendu : la grille existante affiche les produits filtrés par
section, et `ProductCard` affiche le nom, l'image et le prix (`{Binding Price, StringFormat='{0:F2} €'}`).

- Vérifier par les tests de la tâche 1 que la sélection Livres ne montre que les livres
  disponibles et leur prix.
- Risque : un livre sans `UrlImage` affiche une image vide. Accepté (la maquette montre
  des couvertures grises). Ne pas ajouter d'image par défaut dans cette PR.
- **Maquette :** preview/Main.html — zones E2-grille-livres, E2-carte-livre, E2-prix-livre
- Message de commit : `test(cash): grille des livres disponibles`.

## Tâche 5 — Panier avec livres (E3)

Aucun changement de code attendu : `AddToCartCommand` accepte tout `Product`, et `CartTotal`
additionne toutes les lignes.

- Test : ajouter un livre à un panier qui contient déjà un produit buvette ; `CartTotal` vaut
  la somme des deux prix. Utiliser `AddToCartCommand.Execute(product)`.
- Test : ajouter deux fois le même livre augmente la quantité, sans créer de deuxième ligne
  (CL-02).
- **Maquette :** preview/Panier.html — zones E3-ligne-livre, E3-total
- Message de commit : `test(cash): panier mixte buvette et livres`.

## Tâche 6 — État vide (E4)

Fichiers : `src/MauiCashApp/ViewModels/MainViewModel.cs`, `src/MauiCashApp/MainPage.xaml`.

- Ajouter `IsLivresEmptyVisible` : vrai quand la catégorie sélectionnée est Livres, qu'aucun
  livre n'est dans `FilteredProducts` et que le chargement n'est pas en cours (tâche 7).
- Le recalculer quand la catégorie change et quand `ApplyFilter` modifie `FilteredProducts`
  (`OnPropertyChanged`).
- Dans `MainPage.xaml`, un `Label` « Aucun livre disponible pour le moment. » dans la zone de
  la grille, lié à `IsLivresEmptyVisible`.
- Test : sans livre disponible, `IsLivresEmptyVisible` est vrai ; avec un livre, il est faux.
- **Maquette :** preview/Etats.html — zones E4-etat-vide
- Message de commit : `feat(cash): état vide de la grille Livres`.

## Tâche 7 — Chargement (E4)

Fichiers : `src/MauiCashApp/ViewModels/MainViewModel.cs`, `src/MauiCashApp/MainPage.xaml`.

- Ajouter `[ObservableProperty] bool _isLoading`. Le passer à vrai au début de
  `LoadProductsAsync` et à faux dans un `finally`.
- Dans `MainPage.xaml`, un `ActivityIndicator` (`IsRunning="{Binding IsLoading}"`) et le
  texte « Chargement du catalogue… », visibles quand `IsLoading` est vrai et que l'onglet
  Livres est sélectionné (arbitrage A1).
- Test : avec un service qui attend un `TaskCompletionSource`, `IsLoading` est vrai pendant
  l'attente et faux après.
- **Maquette :** preview/Etats.html — zones E4-etat-chargement
- Message de commit : `feat(cash): état de chargement de la grille Livres`.

## Tâche 8 — Hors ligne (E4)

Fichiers : `src/MauiCashApp/Interfaces/IProductService.cs`, `src/MauiCashApp/Services/ProductService.cs`,
`src/MauiCashApp/ViewModels/MainViewModel.cs`, `src/MauiCashApp/MainPage.xaml`,
`src/MauiCashApp.Tests/MainViewModelTests.cs`, `src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`.

- Créer `ProductCatalog` (record) : `IEnumerable<Product> Products` et `bool IsFromCache`.
  Ajouter le fichier à `ShopAppVpd.Tests.csproj` (lien `Production\ProductCatalog.cs`), car
  `IProductService.cs` y est déjà lié.
- `IProductService.GetProductsAsync` renvoie `ProductCatalog`.
- `ProductService` : `IsFromCache = false` seulement après une réponse API réussie. Tous les
  chemins de repli (hors ligne, réponse en échec, exception) renvoient `IsFromCache = true`.
- `MainViewModel` : `[ObservableProperty] bool _isCatalogFromCache`, renseigné par `LoadProductsAsync`.
- Dans `MainPage.xaml`, un bandeau « Hors ligne : catalogue de la tablette » visible quand
  `IsCatalogFromCache` est vrai et que l'onglet Livres est sélectionné (arbitrage A1).
- Mettre à jour `StubProductService` dans `MainViewModelTests.cs` pour renvoyer un `ProductCatalog`.
- Tests : avec `IsFromCache = true`, `IsCatalogFromCache` est vrai et les livres en cache
  restent dans `FilteredProducts` (CL-07).
- **Maquette :** preview/Etats.html — zones E4-bandeau-hors-ligne
- Message de commit : `feat(cash): bandeau hors ligne pour le catalogue en cache`.
- **Piège** : `ProductDatabase.SaveProductsAsync` remplace tout le cache à chaque synchronisation
  réussie. Après déploiement, les livres n'apparaissent hors ligne qu'après une première
  synchronisation réussie avec la nouvelle version. Le noter dans la PR.

## Tâche 9 — Documentation

- `docs/bourse-aux-livres/01-vision-et-perimetre.md` §5 : remplacer la ligne « Intégration à
  l'application MAUI de caisse buvette » par une exclusion limitée aux sorties et aux ventes.
  L'affichage des prix sur la tablette est autorisé.
- `docs/bourse-aux-livres/06-regles-metier.md`, `RG-50` : ajouter « la calculette de la
  tablette affiche les prix, sans rien enregistrer ».
- `.github/memory/04-frontend.md` : retirer la phrase « books are hidden ».
- `src/MauiCashApp/README.md` : retirer « shows only available Buvette and Loto items ».
- Message de commit : `docs(cashier-books): calculette de prix dans la caisse`.

## Tâche 10 — Validation et PR (dernière tâche)

- Build APK : `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android --configuration Release -p:AndroidPackageFormat=apk`.
- Tests : `dotnet test src/MauiCashApp.Tests/ShopAppVpd.Tests.csproj`. Comparer aux échecs notés en tâche 0.
- Validation manuelle sur tablette ou émulateur :
  - l'onglet Livres apparaît après Buvette et Loto ;
  - seuls les livres disponibles apparaissent, avec leur prix ;
  - ajouter un livre au panier met à jour le total ; `VALIDER` vide le panier ;
  - sans livre disponible, l'état vide apparaît ;
  - en mode avion après une synchronisation, les livres restent et le bandeau apparaît.
- Ouvrir la PR depuis `feat/cashier-books` vers `main`, avec la ligne de signature du projet.
- Ne pas publier l'APK (arbitrage A3).
- Message de commit : `chore(cash): validation livres`, seulement si un ajustement est nécessaire.

## Zones non implémentées

Aucune. Toutes les zones des maquettes sont couvertes par une tâche ci-dessus.
