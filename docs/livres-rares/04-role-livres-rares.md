# 04 — Le rôle `LivresRares`

Un rôle applicatif se déclare aujourd'hui à neuf endroits. Les oublier est la façon la
plus simple de livrer un module qui « marche chez moi » et refuse l'accès en production.

## 1. Identité du rôle

| | |
|---|---|
| Valeur Entra | `LivresRares` |
| Nom affiché | `Bénévole livres rares` |
| Description | `Crée et tient à jour les fiches des livres rares : prix, photos, état.` |
| **GUID** | **à générer une fois, puis figé pour toujours** |
| Libellé dans les interfaces | `Livres rares` |
| Couleur | `#5f3a86` |

Le GUID doit être généré une seule fois et copié tel quel dans les deux scripts
PowerShell. `Configure-EntraApps.ps1` le dit explicitement :

> « Les GUID sont fixes : ils identifient les roles et les portees de facon stable d'une
> execution a l'autre, et surtout d'un environnement a l'autre. Ne jamais les
> regenerer : les attributions d'utilisateurs y font reference. »

Générer avec `[guid]::NewGuid()` et le consigner dans la PR.

## 2. Infrastructure Entra

### `infra/entra/Configure-EntraApps.ps1`

Ajouter une quatrième entrée au tableau `$AppRoles` (vers la ligne 109), après
`Administration` :

```powershell
@{
    Id          = '<GUID GÉNÉRÉ UNE FOIS>'
    Value       = 'LivresRares'
    DisplayName = 'Benevole livres rares'
    Description = 'Cree et tient a jour les fiches des livres rares : prix, photos, etat.'
}
```

Mettre à jour aussi le commentaire d'en-tête du script (ligne 10), qui énumère les rôles
existants, et le bloc de commentaire des lignes 106-108.

⚠️ Les scripts du dossier `infra/entra/` sont écrits **sans accents** — respecter cette
convention, elle est volontaire (encodage des consoles PowerShell Windows).

### `infra/entra/Set-VpdUserRole.ps1`

Deux endroits :

- le `[ValidateSet('Tri', 'Caisse', 'Administration')]` de la ligne 57 ;
- la table de correspondance des GUID des lignes 72-74.

Plus la documentation d'en-tête (lignes 9 et 23).

### `infra/entra/Get-VpdUserRoles.ps1`

**Aucune modification.** Il lit les rôles depuis l'enregistrement d'application et suit
donc automatiquement.

## 3. Backend

### `Application/AccountAdministration/AccountRoles.cs`

Ajouter `public const string LivresRares = "LivresRares";` et l'inclure dans les deux
`switch` de `Normalize` et `IsValid`.

> Attention au comportement de `Normalize` : si **un seul** rôle est inconnu, la méthode
> retourne une liste **vide**, pas une liste partielle. Un oubli ici ne provoque pas une
> erreur visible mais un compte silencieusement dépouillé de tous ses droits. Couvrir ce
> cas par un test.

### `Api/Program.cs`

Ajouter aux politiques (autour des lignes 66-74) :

```csharp
.AddPolicy("RareBooks", policy => policy.RequireRole("LivresRares", "Administration", "Admin"))
```

La politique accepte aussi `Administration` : un administrateur doit pouvoir gérer les
livres rares sans qu'on lui attribue un second rôle.

Ne **pas** ajouter `LivresRares` à la politique `ScanVolunteer` (`Tri`, `Caisse`) : le
rôle rare n'ouvre ni le tri ni la vente.

## 4. Catalogue et portail d'administration — `src/Catalog`

| Fichier | Modification |
|---|---|
| `core/catalog-auth.service.ts` | Ajouter `const RARE_BOOKS_ROLES = new Set(['livresrares'])` et un signal calculé `isRareBookManager` sur le modèle d'`isAdministrator` (lignes 43 et 105). Comme partout dans ce service, la comparaison est en minuscules |
| `core/catalog.models.ts:681` | `export type CatalogAdminAccountRole = 'Tri' \| 'Caisse' \| 'Administration' \| 'LivresRares'` |
| `features/administration/catalog-administration-page.component.ts:289` | Ajouter `{value: 'LivresRares', label: 'Livres rares'}` à `accountRoleOptions` — cela alimente à la fois les puces de la liste des comptes et les cases du formulaire de création |
| `core/layouts/navigation/catalog-navigation.component.*` | Le pastille « Livres rares », voir [`05`](05-catalogue-public.md) §5 |
| `core/catalog-administration-route.ts` | Ajouter `'rare-books'` à `CATALOG_ADMIN_SECTIONS` |

Le style de la puce de rôle existe déjà (`.role-chip`) ; ajouter une variante violette.

## 5. Scanette — `src/Scan`

| Fichier | Modification |
|---|---|
| `auth/scan-auth.service.ts` | Ajouter `export const SCAN_RARE_ROLE = 'LivresRares'`, un accesseur `get canManageRareBooks()`, et **étendre `hasScanRole`** pour que le rôle ouvre l'application |
| `auth/scan-auth.service.ts` | `SCAN_REQUIRED_ROLE` vaut aujourd'hui la chaîne `'Tri ou Caisse'`, affichée à l'utilisateur non autorisé. La faire devenir `'Tri, Caisse ou Livres rares'` |
| `scan-routing.ts` | `export type ScanRequiredRole = 'Tri' \| 'Caisse' \| 'LivresRares'`, et étendre `scanRoleGuard` — sa forme actuelle est un ternaire à deux branches (`role === 'Tri' ? auth.canSort : auth.canSell`), à remplacer par une correspondance explicite |
| `scan-routing.ts` | Ajouter les routes `livres-rares`, voir [`07`](07-scanette.md) |
| `scanner.component.html` | La tuile d'accueil, entre `home-mode-tri` et `home-mode-cash` |

⚠️ **Le point à ne pas manquer** : dans `publishAccount` et `retrySilentRenewal`, le
statut `authorized` est calculé par une comparaison en dur sur `SCAN_TRI_ROLE` et
`SCAN_CASH_ROLE`. Un bénévole qui n'a **que** le rôle `LivresRares` serait donc marqué
`unauthorized` et verrait l'écran de refus. Les deux endroits doivent passer par
`hasScanRole`. Le premier (`publishAccount`) n'utilise pas encore cette fonction alors
que le second si : c'est une asymétrie existante du fichier, à corriger en même temps.

De même, `publishDegradedAccount` appelle `hasScanRole` pour décider si la session hors
ligne est récupérable — étendre la fonction suffit à couvrir ce cas.

## 6. Documentation métier

- `docs/bourse-aux-livres/05-administration.md` — ajouter le rôle au tableau des droits.
- `docs/bourse-aux-livres/06-regles-metier.md` — `RG-40` énumère les rôles bénévoles.
- `docs/bourse-aux-livres/02-glossaire-et-cycle-de-vie.md` — l'entrée « Livre rare »
  décrit encore un indicateur ; la réécrire autour de la fiche.

## 7. Test d'acceptation du rôle

Sur un environnement de développement, avec un compte ne portant **que** `LivresRares` :

1. `Set-VpdUserRole.ps1 -Role LivresRares` réussit.
2. Le jeton contient bien `"roles": ["LivresRares"]`.
3. La scanette s'ouvre, l'accueil montre **la seule** tuile « Livres rares » — ni tri,
   ni caisse.
4. `/administration` ouvre la vue restreinte à une section
   ([`06`](06-portail-administration.md) §5).
5. L'en-tête du catalogue affiche la pastille violette « Livres rares » et **pas**
   « Administration ».
6. `GET /rare-books/admin` répond `200`, `GET /books/admin/books` répond `403`.
