# Audit Catalogue + Scanette — bugs, UI/UX et visuel

**Date :** 8 septembre 2026
**Périmètre :** `src/Scan/` (PWA Scanette), `src/Catalog/` (catalogue public + administration) et les endpoints de `src/Backend/` que ces deux applications consomment.
**Méthode :** lecture statique du code (composants, services, templates, SCSS, config PWA/SSR/CI, contrôleurs, handlers, configurations EF, `Program.cs`, paramètres d'infrastructure). Aucun test d'exécution n'a été lancé.
**Destinataire :** agent chargé des correctifs.

## Comment lire ce document

Chaque anomalie porte un identifiant (`SCAN-xx` pour la Scanette, `CAT-xx` pour le Catalogue, `API-xx` pour le backend), une sévérité, un symptôme observable, la cause localisée dans le code, et un correctif concret.

| Sévérité | Signification |
|---|---|
| **P1** | Perte de données, impasse fonctionnelle, ou non-conformité. À traiter avant la prochaine bourse. |
| **P2** | Bug visible qui dégrade fortement l'usage ou l'information affichée. |
| **P3** | Défaut d'UI/UX, incohérence visuelle, dette de qualité. |

**Ordre de traitement recommandé.** Trois lots, dans cet ordre :

1. **Sécurité et intégrité des données** — API-01 (contrôle de propriété), API-02 + API-03 (reprise hors ligne), API-07 (propagation des ventes), SCAN-03 (changement d'utilisateur), CAT-01 (consentement), CAT-02 (crash au démarrage).
2. **Impasses fonctionnelles en bourse** — SCAN-01, SCAN-04, SCAN-02, SCAN-05, API-06, API-11.
3. **Charge et coût** — API-04, API-05, API-08, API-10 : ces quatre points sont le principal moteur de consommation SQL/CPU et méritent d'être traités ensemble.

Le reste (P2 puis P3) peut être réparti librement. **Plusieurs anomalies décrivent le même défaut sous trois angles et doivent être corrigées d'un bloc :**
- **Règle « livre recherché » :** SCAN-10 + SCAN-11 + API-09.
- **Fuseaux horaires :** CAT-04 + CAT-15 + API-14 + API-16 (`/asso-events`) — un utilitaire Europe/Paris partagé les résout tous.
- **Contrastes :** SCAN-18 + CAT-12 (même palette, mêmes valeurs).

---

# Partie 1 — Scanette (`src/Scan/`)

## P1 — Bloquants

### SCAN-01 · La caméra reste morte après un scan qui échoue (impasse en mode Tri)

**Symptôme.** En mode Tri, si le code-barres lu n'est pas un ISBN valide (EAN produit, code interne, code abîmé), ou si l'écriture locale échoue, l'écran affiche un message d'erreur et **la caméra ne redémarre jamais**. Le texte à l'écran dit « La caméra va s'activer automatiquement » alors que rien ne se passe. Le bénévole est bloqué : il doit revenir à l'accueil et rouvrir une session.

**Cause.**
- `src/Scan/src/app/scanner/scanner.component.ts:1157` — `handleCameraDetection()` désactive la caméra puis appelle `lookup()`, et enchaîne sur `restartContinuousCamera(destination)`.
- `src/Scan/src/app/scanner/scanner.component.ts:1166` — `restartContinuousCamera()` **retourne immédiatement quand `destination === 'tri'`**. En mode Tri, le redémarrage caméra est délégué à `decideCurrentScan()` → `resumeCamera()`.
- Or `decideCurrentScan()` n'est atteignable que si `localScan` existe. Quand `lookup()` échoue (ISBN invalide, `recordScan()` en erreur), `localScan` reste `null`, aucun bouton « Garder / Écarter » n'est rendu, donc `resumeCamera()` n'est jamais appelé.
- Aucun bouton de secours : `camera-retry` (`scanner.component.html:196`) n'est affiché que si `cameraError` est renseigné, pas si `errorMessage` ou `storageError` le sont.

**Correctif.**
1. Dans `restartContinuousCamera()`, autoriser la reprise en mode `tri` lorsqu'aucun scan exploitable n'a été produit :
   ```ts
   private restartContinuousCamera(destination: ScanDestination): void {
     if (this.screen !== destination) {
       return;
     }
     // En tri, la caméra est normalement relancée par la décision ; sans scan
     // exploitable il n'y aura pas de décision, il faut donc reprendre ici.
     if (destination === 'tri' && this.localScan !== null) {
       return;
     }
     this.resumeCamera();
   }
   ```
2. Élargir la condition d'affichage du bouton de secours dans `scanner.component.html:196` :
   `*ngIf="cameraError || errorMessage || storageError"` avec un libellé « Reprendre le scan ».

**Test de non-régression.** Scanner un EAN non-livre (ex. `3560070462438`) en mode Tri → l'erreur s'affiche **et** la caméra se réarme dans les 2 s.

---

### SCAN-02 · Message d'erreur « Saisissez un ISBN… » après un scan caméra

**Symptôme.** Un code-barres non-ISBN lu par la caméra ou par la douchette affiche « **Saisissez un ISBN-10 ou ISBN-13 valide.** » alors que le bénévole n'a rien saisi. Message incompréhensible en situation.

**Cause.** `scanner.component.ts:372` — `lookup()` produit le même message quelle que soit l'origine (caméra, douchette, pavé numérique).

**Correctif.** Passer l'origine à `lookup()` et différencier :
- caméra/douchette → « Ce code-barres n'est pas un ISBN. Il s'agit peut-être d'un code produit : saisissez l'ISBN à la main. »
- saisie manuelle → message actuel.

Ajouter aussi, dans ce cas, un raccourci direct vers `openManualInput()` pré-rempli avec la valeur lue.

---

### SCAN-03 · Le changement d'utilisateur ne purge pas les données locales

**Symptôme.** « Changer d'utilisateur » sur l'accueil, puis connexion d'un autre bénévole : la file d'attente (outbox), la session de tri en cours **et le panier de caisse non validé du bénévole précédent restent en place**. À la synchronisation suivante, les gestes du bénévole A sont envoyés au serveur sous le jeton du bénévole B (le serveur attribue le `volunteerId` d'après le token). Attribution des scans faussée et fuite de contexte entre bénévoles sur un appareil partagé — ce qui est le mode d'usage normal en bourse.

**Cause.**
- `scanner.component.ts:596` — `logout()` ne fait que `scanAuth.logout()` + `screen = 'home'`. `cashItems`, `localScan`, la session IndexedDB et l'outbox sont intacts.
- `scan-auth.service.ts` — `logout()` purge l'autorisation locale mais pas `ScanLocalStoreService`.
- L'outbox n'est associée à aucun compte : `ScanSessionSnapshot.volunteerId` vaut `null` jusqu'à la synchronisation.

**Correctif.**
1. Avant la déconnexion, si `pendingTransmissionCount > 0` ou `pendingDecisionCount > 0`, **bloquer** avec un message explicite : « N gestes ne sont pas encore transmis. Synchronisez avant de changer d'utilisateur. » avec un bouton « Synchroniser maintenant ».
2. Une fois la file vide, purger l'état local dans `logout()` :
   ```ts
   async logout(): Promise<void> {
     if (this.pendingDecisionCount > 0 || this.pendingTransmissionCount > 0) { /* blocage ci-dessus */ return; }
     this.cashItems = [];
     this.cashMessage = null;
     this.lastValidatedSaleIds = [];
     this.resetLookupState();
     await this.scanWorkflow?.clearSession();
     this.scanAuth?.logout();
   }
   ```
3. Renseigner `volunteerId` dans la session locale dès l'authentification (`account.homeAccountId`) et **refuser la transmission** d'une session dont le `volunteerId` diffère du compte connecté (la mettre en `Orphaned`, comme le fait déjà `orphanPendingOutboxEntriesFromOtherSessions`).

---

### SCAN-04 · Le bandeau « mode dégradé » casse la mise en page

**Symptôme.** Quand la session ne peut plus être renouvelée (réseau instable — exactement le cas d'usage cible), le bandeau jaune s'affiche **à côté** de l'application au lieu d'être au-dessus, et écrase la colonne de 390 px. L'écran devient inutilisable.

**Cause.**
- `src/Scan/src/styles.scss:62` — `.scan-shell { display: flex; justify-content: center; }` sans `flex-direction`, donc **en ligne**.
- `scanner.component.html:26` — `.auth-degraded-banner` est un enfant direct de `.scan-shell`, frère de `.app-screen`. Les deux se partagent la ligne.
- `scanner.component.scss` — le bandeau n'a ni `position`, ni `width`, ni `flex-basis`.

**Correctif.** Sortir le bandeau du flux de `.scan-shell` :
```scss
.auth-degraded-banner {
  position: fixed;
  z-index: 50;
  top: 3px;                       /* sous le liseré .app-screen::before */
  right: 0;
  left: 0;
  width: min(100%, 390px);
  margin: 0 auto;
  border-radius: 0;
  /* … reste inchangé */
}
```
et prévoir un décalage haut équivalent sur `.app-screen` quand la classe est active, sinon le bandeau recouvre le `mode-header`.

---

### SCAN-05 · Le cache hors ligne des notices bibliographiques ne fonctionne pas

**Symptôme.** L'application est annoncée « offline-first », mais aucune notice (titre / auteur / couverture) n'est disponible hors ligne, même pour un ISBN déjà scanné dans la journée.

**Cause.** `src/Scan/ngsw-config.json` — le `dataGroup` `bibliographic-metadata` déclare `"/books/**/metadata"`. Les motifs commençant par `/` du service worker Angular ne matchent que les requêtes **de même origine**. Or l'API est sur une autre origine (`API_URL` injecté au build par `.github/workflows/scan-deploy.yml`, `https://vpd-api-ca-dev….azurecontainerapps.io`). Le motif ne matche jamais, rien n'est mis en cache.

**Correctif.**
```json
"urls": ["**/books/**/metadata"]
```
(motif absolu, valable quelle que soit l'origine de l'API). Vérifier après déploiement dans DevTools → Application → Cache Storage qu'un groupe `bibliographic-metadata` se remplit.

**Dans la même config, deux autres manques :**
- Les polices Google (`index.html:10`) ne sont ni auto-hébergées ni mises en cache. **Hors ligne, toute la typographie retombe sur Arial** — régression visuelle immédiate sur l'écran de tri. Le Catalogue, lui, auto-héberge ses polices (`src/Catalog/src/styles.scss:1-30`) : appliquer la même approche à la Scanette (copier les `.woff2` dans `public/fonts/`, déclarer les `@font-face` dans `styles.scss`, supprimer le `<link>` Google).
- Aucun usage de `SwUpdate` : un appareil laissé ouvert toute la journée ne prendra jamais un correctif publié pendant la bourse. Ajouter un écouteur `versionUpdates` qui propose « Une nouvelle version est disponible — recharger », en dehors d'une session de tri en cours.

---

## P2 — Bugs visibles

### SCAN-06 · La douchette est ignorée après un clic sur un bouton

**Symptôme.** Après avoir cliqué « Garder » ou « Écarter », la lecture au pistolet ne déclenche plus rien. Il faut cliquer ailleurs dans la page pour la réactiver.

**Cause.** `scanner.component.ts:1255` — `isEditableTarget()` inclut `target.tagName === 'BUTTON'` dans la liste des cibles à ignorer. Après un clic, le focus reste sur le bouton, donc `onDocumentKeydown()` ignore toutes les frappes de la douchette.

**Correctif.** Retirer `BUTTON` de `isEditableTarget()` (un bouton n'est pas une zone de saisie) :
```ts
return target instanceof HTMLElement && (
  target.tagName === 'INPUT' ||
  target.tagName === 'TEXTAREA' ||
  target.isContentEditable
);
```
et, par sécurité, appeler `(event.target as HTMLElement)?.blur()` après une décision.

**Bug voisin, même fonction (`scanner.component.ts:987`) :** le tampon est vidé *avant* le test de `Enter`. Si la douchette met plus de 120 ms entre le dernier chiffre et le `Enter` (fréquent en Bluetooth), le scan est **perdu silencieusement**. Correctif : ne vider le tampon que pour les touches de données, pas pour `Enter`, et porter la fenêtre à 300 ms.

---

### SCAN-07 · Le cadre caméra recouvre l'état de synchronisation

**Symptôme.** Sur mobile, le panneau caméra masque le bandeau de synchronisation et parfois la barre de session. Les messages d'erreur de synchro (« N gestes sans décision isolés », « entrées en quarantaine ») deviennent invisibles pendant le scan — or ce sont précisément les alertes à ne pas rater.

**Cause.** `src/Scan/src/styles.scss:1674` — `.camera-engine-host-active { position: fixed; top: 92px; z-index: 40; }`, valeur en dur. Or la pile au-dessus est variable :
- `.mode-header` (~52 px)
- `.sync-panel` qui, sous 34 rem (**tous les téléphones**), passe en colonne avec bouton pleine largeur (`scanner.component.scss`, media query finale) et grandit d'une ligne par état actif (hors ligne, décisions en attente, gestes à transmettre, message d'erreur) : de 70 px à ~160 px
- `.session-bar` (~42 px)

**Correctif.** Ancrer la caméra sur le flux plutôt que sur une constante :
- soit rendre `.camera-engine-host` enfant de `.tri-body` en `position: absolute; inset: 0` (relatif à `.tri-body` qui devient `position: relative`) ;
- soit, a minima, calculer le décalage : donner à la pile d'en-tête `position: sticky` et mesurer sa hauteur en JS (`ResizeObserver`) pour alimenter une variable CSS `--scan-header-height`, puis `top: var(--scan-header-height)`.

**Note visuelle liée :** le réticule décoratif `.scan-reticle` (`scanner.component.html:172`) est dessiné dans `.scan-stage`, donc **à un endroit qui ne correspond pas au cadrage réel de la caméra**. Deux viseurs désalignés à l'écran. Supprimer `.scan-reticle` quand `cameraActive` est vrai, et ne garder que le viseur du panneau caméra (`.camera-engine-host-active::after`).

---

### SCAN-08 · Le verdict est clipé sur les petits écrans (contenu inaccessible)

**Symptôme.** Sur un écran ≤ ~700 px de haut (iPhone SE, Android d'entrée de gamme, navigateur avec barre d'URL visible), la fiche de verdict est **coupée en bas sans possibilité de défiler** : les compteurs « Déjà vendus » et « Recherché par », et parfois la note d'erreur, sont hors écran.

**Cause.**
- `src/Scan/src/styles.scss:524` — `.tri-body { overflow: hidden; }`
- `src/Scan/src/styles.scss:789-792` — `.tri-result { flex: 1 0 auto; }` (`flex-shrink: 0`, donc pas de compression) sans conteneur défilant.

Hauteur cumulée du verdict : carte verdict (~110) + carte livre (138) + 4 lignes de faits (176) + notes ≈ 430 px, en plus des ~170 px d'en-têtes et des ~120 px du dock de décision.

**Correctif.**
```scss
.tri-body { overflow-y: auto; -webkit-overflow-scrolling: touch; }
.tri-result { flex: 1 1 auto; min-height: 0; }
```
Le même défaut existe sur l'écran **Consultation** : ses blocs (`book-card`, `book-facts`, `consultation-notice`) sont enfants directs de `.app-screen` qui est en `overflow: hidden`. Envelopper le contenu de consultation dans un `<div class="consultation-body">` avec `flex: 1 1 auto; min-height: 0; overflow-y: auto;`.

---

### SCAN-09 · Le titre d'un livre connu peut être effacé par la notice bibliographique

**Symptôme.** Un livre correctement titré dans le catalogue local s'affiche ensuite « Titre non renseigné » sur la fiche de tri et dans la ligne de caisse.

**Cause.** `src/Scan/src/app/offline/scan-workflow.service.ts:354` — `cacheMetadata()` écrit `title: metadata.title` et `workId: metadata.workId` **sans repli sur la valeur existante**. Si le service de métadonnées renvoie une notice partielle (`title: null`), la donnée du catalogue est écrasée par `null`.

**Correctif.**
```ts
title: metadata.title ?? existing?.title ?? null,
authors: metadata.authors ?? existing?.authors ?? null,
workId: metadata.workId ?? existing?.workId ?? null,
```

---

### SCAN-10 · Le compteur « Recherché par » est un booléen déguisé en nombre

**Symptôme.** La fiche affiche toujours « Recherché par : **1** » et « 1 personne le recherche », même si dix membres suivent le titre. Le pluriel géré dans `verdictSummary` (`scanner.component.ts:293`) ne se déclenche jamais.

**Cause.** `src/Scan/src/app/offline/scan-verdict.service.ts:26` — `const activeRequesterCount = book?.isWanted ? 1 : 0;`. Le delta catalogue (`ScanCatalogBook`) ne transporte qu'un drapeau `isWanted`, pas un décompte.

**Correctif.** Deux options selon l'ambition :
- **Minimale** (sans toucher au backend) : remplacer l'affichage numérique par un état binaire — « Recherché » / « — » dans `book-facts`, et « Au moins une personne recherche ce titre » dans le résumé. Cela supprime une information fausse.
- **Complète** : ajouter `activeRequesterCount: int` à `ScanCatalogBookResult` (`src/Backend/…/Books/Common/ScanCatalogBookResult.cs`) et le propager jusqu'au delta, puis lire la vraie valeur côté client.

---

### SCAN-11 · Le verdict « Inutile d'en garder » est quasi inatteignable

**Symptôme.** Un titre déjà présent en 20 exemplaires s'affiche « **À garder** » dès qu'une seule vente a été enregistrée. Le verdict `TooMany`, censé être le principal outil de désengorgement, ne se déclenche presque jamais.

**Cause.** `scan-verdict.service.ts:33-39` — l'ordre de priorité est `Wanted` > `Selling` > `TooMany`, et `demandSalesThreshold` vaut **1** par défaut (`scan-verdict.service.ts:12`, et `settingsForm.demandSalesThreshold = 1` côté administration). Tout livre ayant connu une vente passe en `Selling`.

**Correctif.** Faire primer le stock sur la demande quand le stock est manifestement excédentaire :
```ts
const verdict = activeRequesterCount > 0
  ? 'Wanted'
  : totalKnownQuantity >= duplicateThreshold
    ? 'TooMany'                                  // le sur-stock l'emporte
    : salesCount >= demandSalesThreshold
      ? 'Selling'
      : 'FirstCopy';
```
À valider avec la présidente : c'est une règle métier, pas seulement un bug technique. Documenter le choix dans `docs/bourse-aux-livres/06-regles-metier.md`.

---

### SCAN-12 · Aucun retour quand la connexion échoue depuis l'accueil

**Symptôme.** Le bouton « Se connecter comme bénévole » du pied d'accueil peut ne rien faire, sans message.

**Cause.** `scanner.component.ts:590` — `login()` souscrit avec `error: () => undefined`, l'erreur est avalée. (Le composant `ScanLoginComponent`, lui, gère correctement `loginError`.)

**Correctif.** Réutiliser le même traitement : renseigner `errorMessage` (« Impossible de démarrer la connexion. Réessayez. ») et déclencher `refreshView()`.

---

### SCAN-13 · Les requêtes réseau n'ont pas de délai maximal

**Symptôme.** En réseau très dégradé (portail captif, 3G saturée en salle des fêtes), une requête peut rester en attente indéfiniment : le statut reste bloqué sur « Synchronisation… » et le bouton « Synchroniser » est désactivé pour toujours.

**Cause.** Aucun opérateur `timeout()` dans `src/Scan/src/app/offline/scan-api.service.ts` ni dans `book-metadata.service.ts`. `syncNow()` (`scanner.component.ts:601`) attend `this.syncPromise` sans échéance.

**Correctif.** Ajouter un intercepteur ou un `timeout()` par appel :
```ts
return this.http.get<…>(url, {params}).pipe(timeout({ first: 15_000 }));
```
15 s pour le catalogue et les gestes, 8 s pour les notices bibliographiques (best-effort). Les erreurs `TimeoutError` doivent être classées `transient` par `classifyFailure()` (`scan-sync.service.ts`) pour bénéficier du backoff existant.

---

## P3 — UI / UX / visuel

### SCAN-14 · Pas de retour à l'accueil depuis Tri ni depuis le choix de mode

Les écrans `session-mode` et `tri` n'offrent aucun chemin de retour : « Terminer » (`scanner.component.html:157`) clôt la session. Un bénévole qui ouvre le tri par erreur doit soit terminer une session vide, soit recharger la page. Les écrans Caisse et Consultation ont bien un bouton « Accueil ».
→ **Correctif :** ajouter un « Accueil » dans le `session-bar` du tri (avec confirmation si `scannedCount > 0`) et une flèche retour dans l'en-tête de `session-mode`.

### SCAN-15 · Le bouton « Photo » n'est pas accessible au clavier

`scanner.component.html:262` — `<label class="dock-photo" for="image-input">Photo</label>`. Un `<label>` n'est pas focusable : impossible d'y accéder au clavier ou en navigation lecteur d'écran, alors que les deux autres actions du dock sont des `<button>`.
→ **Correctif :** remplacer par un `<button type="button" (click)="imageInput.click()">` avec `#imageInput` sur l'input fichier. Ajouter aussi cette action aux docks Caisse et Consultation, qui en sont dépourvus.

### SCAN-16 · `aria-labelledby` pointe vers un élément absent

`scanner.component.html:145` — la `<section class="tri-screen" aria-labelledby="tri-title">` référence `#tri-title`, qui n'existe que dans la branche « pas encore de scan ». Dès qu'un verdict s'affiche, la section n'a plus de nom accessible.
→ **Correctif :** basculer sur `[attr.aria-labelledby]="localScan || metadata ? 'verdict-title' : 'tri-title'"`.

### SCAN-17 · Le panneau de synchronisation est hors charte

Le reste de la Scanette suit une charte éditoriale plate : angles droits, libellés mono en capitales, palette `--scan-*`. Le panneau de synchro et le bandeau dégradé, eux, sont définis dans `scanner.component.scss` avec `border-radius: 0.75rem`, et des couleurs hors palette (`#f3f8fb`, `#c9d8e2`, `#173244`, `#fff8db`, `#e2b93b`) qui ne correspondent à aucun token `:root`. Marges `0.75rem 1rem` (16 px) contre 20-22 px partout ailleurs : le bord gauche est visiblement désaligné.
→ **Correctif :** déplacer ces règles dans `styles.scss`, utiliser `--scan-line`, `--scan-blue-soft`, `--scan-ink`, `--scan-orange`, supprimer les `border-radius` et aligner les marges horizontales sur 20 px.

### SCAN-18 · Contrastes insuffisants (WCAG AA)

Mesures effectuées sur la palette réelle :

| Couleur | Fond | Ratio | Exigence | Usage |
|---|---|---|---|---|
| `#9dc2da` | `#fff` | **1,88:1** | 4,5:1 | `.book-isbn` (fiche de tri, fiche consultation) |
| `#6d8ba2` (`--scan-muted`) | `#fff` / `#f7fbfe` | **3,58 / 3,44:1** | 4,5:1 | `.dock-hint` (10 px), `.book-facts span` (11 px), `.result-note` (10 px), `.cash-item p` |
| `#fff` | `#dc6412` (`--scan-orange`) | **3,57:1** | 4,5:1 | `.dock-primary`, `.decision-keep` — **le bouton d'action principal** (18 px/700, sous le seuil « grand texte » de 18,66 px gras) |
| `#1497d6` | `#fff` | **3,27:1** | 4,5:1 | `.dock-photo`, `.outline-pill` |

→ **Correctif :** assombrir les valeurs de texte. Pistes validées : `--scan-muted` → `#4e6c84` (5,52:1, déjà dans la palette), `.book-isbn` → `--scan-muted` corrigé, orange de fond des boutons → `#b34c06` (≈4,6:1 avec du blanc) **ou** conserver `#dc6412` en portant le libellé à `19px/700` (au-dessus de 18,66 px gras, seuil « grand texte » : 3:1 suffit alors). Le bleu de lien → `#0c6ea6` (4,7:1).

### SCAN-19 · Manifeste PWA et intégration iOS incomplets

- `src/Scan/public/manifest.webmanifest` ne déclare qu'une icône **SVG**. iOS ne sait pas l'utiliser : une Scanette installée sur iPad/iPhone affiche une capture d'écran en guise d'icône.
- `src/Scan/src/index.html` n'a ni `apple-touch-icon`, ni `apple-mobile-web-app-capable`, ni `viewport-fit=cover`.
- Aucune utilisation de `env(safe-area-inset-*)` dans `styles.scss` : sur un appareil à encoche/indicateur d'accueil, les docks d'action collés en bas (`.action-dock`, `padding-bottom: 24px`) passent partiellement sous l'indicateur.

→ **Correctif :** générer des PNG 192/512 (`purpose: "any"` et `"maskable"` séparés), ajouter `<link rel="apple-touch-icon">`, `viewport-fit=cover`, `"orientation": "portrait"` dans le manifeste, et `padding-bottom: calc(24px + env(safe-area-inset-bottom))` sur `.action-dock`.

### SCAN-20 · Divers

- **Cache-Control sur `index.html`** — `src/Scan/nginx.conf` ne force `no-cache` que sur `ngsw-worker.js` et `ngsw.json`. `index.html` est servi via `try_files` sans en-tête, donc soumis au cache heuristique : un appareil peut rester épinglé sur une ancienne coquille. Ajouter `location = /index.html { add_header Cache-Control "no-cache"; }`.
- **Résolution caméra non contrainte** — `camera-scanner.service.ts:56`, `CAMERA_CONSTRAINTS` ne demande que `facingMode: 'environment'`. Beaucoup d'appareils livrent alors du 640×480, ce qui rend un EAN-13 fin difficile à décoder. Ajouter `width: {ideal: 1920}, height: {ideal: 1080}`. Retirer aussi `BarcodeFormat.QR_CODE` de `CAMERA_SCAN_FORMATS` (`camera-scanner.service.ts:58`) : inutile pour des livres, il ralentit chaque tentative de décodage.
- **Validation de fin de session dans le mauvais bandeau** — `scanner.component.ts:661` place « Choisissez Garder ou Écarter… » dans `syncError`, donc dans le panneau de synchronisation, alors que c'est une erreur de validation métier. Utiliser un champ dédié affiché près du dock de décision.
- **La touche `X` est acceptée partout au pavé** — `appendManualKey()` (`scanner.component.ts:545`) autorise `X` à n'importe quelle position, alors qu'elle n'est valide qu'en 10ᵉ caractère d'un ISBN-10. Désactiver la touche tant que `manualDigitCount !== 9`.
- **Textes en `content:` CSS** — `styles.scss:1717-1740`, les libellés du panneau caméra (« Touchez l'image pour refaire la mise au point », etc.) sont dans des `content:`. Non traduisibles, non sélectionnables, et invisibles pour certains lecteurs d'écran alors qu'ils dupliquent déjà `cameraFocusAriaLabel`. Les passer dans le template.
- **Le panneau de synchro est dupliqué 4 fois** dans `scanner.component.html` (lignes 143, 302, 430, 519) à l'identique. Extraire un `<ng-template #syncPanel>` ou un composant : toute correction doit aujourd'hui être répliquée quatre fois.

---

# Partie 2 — Catalogue (`src/Catalog/`)

## P1 — Bloquants

### CAT-01 · Google Maps chargé sans consentement (RGPD)

**Symptôme.** L'iframe Google Maps de la page d'accueil et de « Les prochaines dates » se charge **avant et indépendamment** de toute décision sur les cookies. Google dépose des identifiants et reçoit l'IP du visiteur dès l'arrivée sur la page.

**Cause.**
- `src/Catalog/src/app/features/home/catalog-home-page.component.html:56` — l'iframe est rendue dès que `address(fair)` est renseignée, sans condition de consentement.
- `catalog-home-page.component.ts:139` — `mapsEmbedUrl()` utilise `bypassSecurityTrustResourceUrl` sur `google.com/maps?…&output=embed`.
- Le bandeau (`catalog-cookie-banner.component.html:8`) ne mentionne que Clarity et GA4, et affirme pour les cookies nécessaires : « **Aucun cookie tiers n'est déposé pour cette finalité** ». L'affirmation est fausse en l'état.

**Correctif.**
1. Ajouter une finalité « Carte / contenus tiers » dans `CookiePreferences` (`cookie-consent.service.ts:13`) et l'exposer dans le panneau de personnalisation.
2. Remplacer l'iframe par un aperçu statique cliquable tant que le consentement n'est pas donné :
   ```html
   @if (consent.preferences.maps) {
     <iframe [src]="mapsEmbedUrl(fair)" …></iframe>
   } @else {
     <button type="button" class="map-consent-placeholder" (click)="consent.enableMaps()">
       Afficher la carte Google Maps <small>Un cookie tiers Google sera déposé.</small>
     </button>
   }
   ```
   Le lien « Ouvrir dans Maps » (`:62`) reste disponible sans consentement : c'est une navigation volontaire.
3. Mettre à jour la politique de cookies (`legal-page.component.ts`) pour déclarer Google Maps.

---

### CAT-02 · Le catalogue plante au démarrage si `localStorage` est inaccessible

**Symptôme.** Sur un navigateur qui refuse le stockage (Safari en navigation privée sur certaines versions, mode « bloquer tous les cookies », politique d'entreprise), **la page reste blanche**.

**Cause.** `src/Catalog/src/app/shared/services/cookie-consent.service.ts:73` — `readStored()` appelle `localStorage.getItem(STORAGE_KEY)` **hors** du bloc `try`. Le `try/catch` n'entoure que le `JSON.parse`. `CookieConsentService` est instancié au chargement (via le pied de page), l'exception remonte et casse le bootstrap. Même exposition dans `save()` (`:90`) avec `setItem`, qui peut lever `QuotaExceededError`.

**Correctif.**
```ts
private readStored(): StoredConsent | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) { return null; }
    const parsed: unknown = JSON.parse(raw);
    return isStoredConsent(parsed) ? parsed : null;
  } catch {
    return null;   // stockage indisponible : on retombe sur le bandeau
  }
}
```
et envelopper le `setItem` de `save()` dans un `try/catch` (le choix reste valable pour la session en cours via le champ `preferences`).

---

## P2 — Bugs visibles

### CAT-03 · Une seule date de bourse affichée sur une page intitulée « Les prochaines dates »

**Symptôme.** La page `/prochaines-dates` (titre « Les prochaines dates », accroche « toutes les informations utiles ») n'affiche jamais qu'**une seule** bourse. Si l'association annonce trois dates, les deux suivantes sont invisibles pour le public.

**Cause.** `catalog-home-page.component.ts:159` — `setFairs()` trie puis ne conserve que `sortedFairs[0]` dans le signal `nextFair`. Toutes les autres sont jetées. Le backend, lui, renvoie bien la liste complète des événements à venir (`EventRepository.GetNextEventsAsync`, filtre `DateStart >= today` et `!IsCancelled`).

**Correctif.** Introduire un signal `upcomingFairs = signal<CatalogFair[]>([])` alimenté par la liste triée, garder `nextFair` comme `computed(() => this.upcomingFairs()[0] ?? null)` pour l'accroche d'accueil, et sur `/prochaines-dates` boucler sur `upcomingFairs()` : la première en carte détaillée, les suivantes en cartes compactes (date, ville, lien agenda).

**Robustesse associée :** le client fait aujourd'hui une confiance totale au filtrage serveur. Ajouter un garde-fou `fairs.filter(f => new Date(f.dateEnd ?? f.dateStart) >= startOfToday)` évite d'afficher une bourse passée si le filtre serveur change (il repose sur `DateTimeOffset.Now.Date`, donc sur le fuseau du conteneur, en UTC en production).

---

### CAT-04 · L'export « Ajouter à mon agenda » enregistre le mauvais horaire

**Symptôme.** Le fichier `.ics` télécharge la bourse avec 1 à 2 h de décalage par rapport à l'horaire affiché sur la page.

**Cause.** Convention en deux temps incohérente :
- `catalog-api.service.ts:97` — `combineDateAndWallClock()` fabrique un ISO dont les composants **UTC** portent l'heure civile française (`14:00` civile → `…T14:00:00Z`).
- `catalog-home-page.component.ts:117` — `formatTime()` relit cette valeur en `timeZone: 'UTC'`, donc l'affichage à l'écran est **correct**.
- `catalog-calendar.ts:36` — `toIcsDate()` fait `new Date(value).toISOString()` et écrit un `DTSTART` en UTC réel. L'agenda du visiteur interprète `14:00Z` comme 15:00 (hiver) ou 16:00 (été) à Paris.

**Correctif.** Émettre l'événement en heure locale avec fuseau explicite plutôt qu'en UTC :
```ts
function toIcsLocalDate(value: string): string {
  // Les composants UTC portent l'heure civile française : on les réémet tels quels.
  return value.replace(/[-:]/g, '').replace(/\.\d{3}Z$/, '').replace(/Z$/, '');
}
// …
`DTSTART;TZID=Europe/Paris:${toIcsLocalDate(event.openAt)}`,
`DTEND;TZID=Europe/Paris:${toIcsLocalDate(event.closeAt || …)}`,
```
et inclure un bloc `VTIMEZONE` Europe/Paris, ou à défaut `X-WR-TIMEZONE:Europe/Paris`.

**À traiter en amont si possible :** la convention « heure civile stockée dans les composants UTC » est la source de cette classe de bugs (déjà rencontrée côté back-office). Un ticket dédié pour normaliser le stockage des horaires d'événement serait plus rentable que des rustines successives.

---

### CAT-05 · Le titre de l'onglet et la description restent ceux de la page précédente

**Symptôme.** Après avoir consulté la fiche d'un livre puis être revenu à l'accueil ou à la recherche, l'onglet du navigateur affiche toujours « *Titre du livre · Auteur · Bourse aux livres* ». Les partages et l'historique du navigateur sont trompeurs, et Google Analytics remonte de mauvais `page_title` (`google-analytics.service.ts:111` lit `document.title`).

**Cause.** Seuls `CatalogBookDetailPageComponent` (`:160`) et `CatalogWorkPageComponent` (`:74`) appellent `Title.setTitle()` / `Meta.updateTag('description')`. Aucune des autres pages (accueil, recherche, catalogue, prochaines dates, compte, mentions légales) ne réinitialise ces balises.

**Correctif.** Centraliser dans `AppComponent` (qui gère déjà `robots` sur `NavigationEnd`, `app.component.ts:53`) : une table `route → {title, description}` appliquée à chaque `NavigationEnd`, que les pages de détail surchargent ensuite avec leurs valeurs.

---

### CAT-06 · Résidus SEO laissés dans le `<head>` après navigation

**Symptôme.** Le `<link rel="canonical">` et le `<script id="catalog-book-jsonld">` d'une fiche livre **restent en place** sur toutes les pages visitées ensuite. La page d'accueil déclare alors une URL canonique de livre et un schema.org `Book` qui ne la concernent pas. En cas de fiche introuvable, le JSON-LD du livre précédent reste également affiché.

**Cause.** `catalog-book-detail-page.component.ts:167-193` — `setSeo()` injecte dans `document.head` sans jamais nettoyer ; `ngOnDestroy()` (`:75`) ne supprime rien. Même schéma pour le canonical dans `catalog-work-page.component.ts:78`.

**Correctif.** Retirer les balises dans `ngOnDestroy()` des deux composants, et, quand `notFound` est vrai, poser `robots: noindex` plutôt que de laisser `index, follow`.

---

### CAT-07 · Toute URL inconnue redirige silencieusement vers l'accueil

**Symptôme.** `/livres/inexistant`, une ancienne URL, une faute de frappe : le visiteur atterrit sur l'accueil sans explication, et le serveur répond **200**. Google indexe des pages fantômes (soft 404) et l'utilisateur perd le fil.

**Cause.** `src/Catalog/src/app/app-routing.module.ts:47` — `{path: '**', redirectTo: ''}`.

**Correctif.** Créer une page « 404 — page introuvable » (avec un champ de recherche et des liens utiles), la router sur `'**'`, y poser `robots: noindex`, et faire renvoyer un statut 404 par le SSR (`src/Catalog/src/server.ts`).

---

### CAT-08 · Résultats de recherche périmés lors d'un changement rapide de filtre

**Symptôme.** En enchaînant deux filtres rapidement, la liste peut afficher les résultats de la requête précédente. Et l'écran « Le catalogue arrive… » ne s'affiche pas entre deux recherches : les anciens résultats restent figés sans indication de chargement.

**Cause.** Deux problèmes cumulés dans `catalog-search-page.component.ts` :
1. `:168` — `load()` déclenche `api.search()` en souscription directe, sans `switchMap`. Les réponses ne sont pas annulées ni ordonnées ; la dernière arrivée gagne, pas la dernière demandée.
2. `:169-170` — `this.loading = true; this.error = false;` sont des **champs simples** dans un composant `OnPush` d'une application **zoneless** (`app.module.ts:52`, `provideZonelessChangeDetection()`). Aucune détection de changement n'est planifiée : l'état de chargement n'est peint qu'au retour de la requête, quand `markForCheck()` est enfin appelé. Même défaut pour `externalLoading` (`:194`) et, dans `catalog-book-detail-page.component.ts:55` et `catalog-work-page.component.ts:45`, pour `loading`/`notFound`/`book` réinitialisés dans le `switchMap`.

**Correctif.**
1. Chaîner la recherche sur le flux de paramètres :
   ```ts
   this.route.queryParamMap.pipe(
     tap(params => { this.readParams(params); this.loading = true; this.changeDetector.markForCheck(); }),
     switchMap(() => this.api.search(this.searchParams()).pipe(catchError(() => { this.error = true; return of(null); }))),
     takeUntil(this.destroyed),
   ).subscribe(…)
   ```
2. Plus durable : convertir `loading`, `error`, `response`, `externalLoading`, `externalResponse` en `signal()`, comme le font déjà les pages Compte et Administration. Cela supprime toute la classe de bugs « champ modifié sans `markForCheck` » propre au mode zoneless.

---

### CAT-09 · Le lien « Administration » disparaît pour les administrateurs après rechargement

**Symptôme.** Un administrateur qui recharge la page ne voit plus « Ouvrir l'administration » dans le menu compte, ni le lien mobile, tant qu'il n'a pas déclenché un appel API.

**Cause.** `src/Catalog/src/app/core/catalog-auth.service.ts:180` — `syncFromCache()` termine par `this._roles.set([])`. Les rôles ne sont renseignés que dans `getApiAccessToken()` (`:127`) ou au retour d'une redirection (`:157`). Après un simple rechargement avec un compte déjà en cache, `isAdministrator()` vaut donc `false`.

**Correctif.** Dans `initializeBrowser()`, après `syncFromCache()`, tenter une acquisition silencieuse pour hydrater les rôles :
```ts
if (this._account()) {
  try { await this.getApiAccessToken(); } catch { /* rôles inconnus : la navigation reste publique */ }
}
```
(en veillant à ne pas déclencher de redirection interactive au chargement : intercepter `InteractionRequiredAuthError` sans rediriger dans ce chemin).

---

### CAT-10 · Le menu compte ne se referme pas et n'est pas navigable

**Symptôme.** Une fois ouvert, le menu compte du bandeau reste affiché : ni un clic à l'extérieur, ni la touche `Échap` ne le ferment. Par ailleurs le déclencheur est un lien `routerLink="/compte"` dont la navigation est systématiquement annulée.

**Cause.** `catalog-navigation.component.html:62-71` — `<a [routerLink]="…" (click)="toggleAccountMenu($event)">` avec `event.preventDefault()` (`catalog-navigation.component.ts:112`). Aucun `@HostListener('document:click')` ni `keydown.escape` dans le composant.

**Correctif.** Transformer le déclencheur en `<button type="button" [attr.aria-expanded]="accountMenuOpen()" aria-haspopup="menu" aria-controls="account-popover">`, ajouter la fermeture au clic extérieur et à `Échap`, et rendre le focus au déclencheur à la fermeture. Le tiroir mobile (`:119`) mérite le même traitement : `role="dialog"`, piégeage du focus, fermeture par `Échap`.

**Menu genres associé :** le sous-menu « Catalogue par genre » repose sur le survol plus un drapeau `suppressedGenreMenu` relâché par `mouseenter`/`focusin` (`:15`). Sur écran tactile il n'y a pas de `mouseenter` : après une première tape, le menu reste supprimé jusqu'à un `focusin`. Le remplacer par un `<button aria-expanded>` piloté par un signal, ouvert au clic et au clavier.

---

### CAT-11 · Les filtres de recherche naviguent à chaque flèche du clavier

**Symptôme.** Un utilisateur au clavier qui parcourt la liste « Genre » ou « Disponibilité » déclenche une navigation et un rechargement complet des résultats **à chaque option survolée**.

**Cause.** `catalog-search-page.component.html:37, 46, 54` — `(ngModelChange)="applyFilters()"` sur les `<select>`. En navigation clavier, le modèle change à chaque option.

**Correctif.** Passer sur `(change)` (déclenché à la validation, pas au parcours) plutôt que `ngModelChange`, ou ajouter un bouton « Appliquer les filtres » et ne naviguer qu'à sa validation.

---

### CAT-12 · Contrastes insuffisants sur le catalogue (WCAG AA)

Mêmes valeurs de palette que la Scanette, mêmes échecs :

| Couleur | Fond | Ratio | Usage |
|---|---|---|---|
| `#6d8ba2` (`--catalog-slate`, `--catalog-slate-3`) | blanc | **3,58:1** | textes secondaires, `.admin-table-wrap small` (0,66 rem ≈ 10,5 px) |
| `#9dc2da` (`--catalog-mist`, `--catalog-blue-soft`) | blanc | **1,88:1** | libellés discrets |
| `#f0801c` (`--catalog-orange`) | blanc | **2,69:1** | accents et libellés |
| `#1497d6` (`--catalog-blue-bright`) | blanc | **3,27:1** | liens et éléments d'action |

→ **Correctif :** réserver `#9dc2da` et `#f0801c` aux surfaces et filets décoratifs (jamais au texte), remonter le texte secondaire à `#4e6c84` (5,52:1) et les liens à `#0c6ea6` (4,7:1). Le site expose une page « Accessibilité » : la déclaration doit refléter l'état réel après correction.

---

### CAT-13 · Colonne « Vendus » vide dans le bilan des bourses

`catalog-administration-page.component.html:188` — la table des bourses déclare un en-tête « Vendus » dont la cellule est le littéral `—` pour toutes les lignes. La donnée n'est pas récupérée (`CatalogAdminFair` ne la porte pas ; il faut ouvrir le détail pour l'obtenir).
→ **Correctif :** soit exposer `soldQuantity` dans `CatalogAdminFair` côté API, soit retirer la colonne. Une colonne vide en production laisse penser que les ventes ne sont pas enregistrées.

---

### CAT-14 · Un message de succès survit à l'échec suivant

`catalog-administration-page.component.ts:869` — `run()` réinitialise `errorMessage` mais **pas** `successMessage`. Après une action réussie puis une action échouée, l'écran affiche simultanément « Les métadonnées ont été enregistrées. » et le message d'erreur. Ambigu sur une surface qui manipule du stock.
→ **Correctif :** appeler `this.clearFeedback()` au début de `run()`, et faire disparaître le message de succès au bout de quelques secondes ou au changement de section.

---

### CAT-15 · Le filtre de dates des sessions décale d'un jour

`catalog-administration-page.component.ts:517-518` — les bornes sont construites en UTC (`new Date(\`${this.sessionFrom}T00:00:00Z\`)`) alors que tous les affichages utilisent `Europe/Paris` (`formatDate`, `:790`). Une session ouverte à 00:30 heure de Paris est datée 22:30 UTC la veille : elle sort du filtre du jour où elle apparaît pourtant dans la colonne « Dernier scan ».
→ **Correctif :** construire les bornes en heure de Paris (décalage `+01:00`/`+02:00` selon la date, ou via une fonction utilitaire partagée). Le même utilitaire servira à CAT-04.

---

## P3 — UI / UX / visuel

### CAT-16 · Textes de remplissage visibles en production

- `catalog-administration-page.component.html:18` — chaque entrée de la navigation d'administration affiche le même sous-titre `« données de l'API »` (sauf « Désengorgement »). Le champ `hint` de `CatalogAdminNavItem` (`:59`) existe mais n'est jamais renseigné. Rédiger un vrai sous-titre par espace de travail, ou supprimer la ligne.
- `:27` et `:173` — notes internes de développement laissées à l'écran : « Cartons & rôles : gestion dédiée à prévoir côté API », « Le backend ne fournit pas encore le volume des cartons… ». À reformuler pour un lecteur bénévole ou à déplacer dans la documentation.
- `catalog-home-page.component.html:136` — quand `recentTotal()` vaut 0, l'accroche affiche « Le catalogue est en cours de mise à jour », y compris lorsque le catalogue est simplement vide ou que l'API a échoué. Distinguer les trois cas (chargement / vide / erreur).
- `:167` — « Voir les 0 titres » quand le catalogue est vide.

### CAT-17 · Libellé accessible incorrect sur le timbre de date

`catalog-home-page.component.html:15` — `[attr.aria-label]="'Le ' + formatFairDateRange(fair)"`. Pour une bourse sur deux jours, `formatFairDateRange()` renvoie « Du 12 mars au 13 mars », d'où un libellé lu « **Le Du 12 mars au 13 mars** ».
→ **Correctif :** produire le libellé complet dans une méthode dédiée, ou supprimer le préfixe.

### CAT-18 · Divers

- **`aria-label` sur la carte livre masque son contenu** — `book-card.component.html:1`, le `<a>` porte `[attr.aria-label]="book.title"`, ce qui remplace intégralement le contenu annoncé : auteur, disponibilité et signal « rare » deviennent inaudibles au lecteur d'écran. Retirer l'`aria-label` (le contenu du lien suffit) ou l'enrichir.
- **Libellé de quantité incomplet** — `book-card.component.ts:31`, `announcedLabel()` produit « 2 à partir du 12 mars » : le nom manque. Écrire « 2 exemplaires à partir du 12 mars ».
- **Lien interne en `href` brut** — `catalog-cookie-banner.component.html:45`, `<a href="/politique-de-cookies">` provoque un rechargement complet de l'application depuis le bandeau. Utiliser `routerLink`.
- **« Gérer les cookies » est un lien qui ne navigue pas** — `catalog-footer.component.html:29`, `<a href=… (click)="…; $event.preventDefault()">`. En faire un `<button class="footer-link-button">`.
- **Année de copyright figée** — `catalog-footer.component.html:35`, `© 2026` en dur. Utiliser l'année courante.
- **Date de mise à jour légale figée** — `legal-page.component.ts:38`, `LAST_UPDATED_LABEL = '7 septembre 2026'` en dur, sans mécanisme de rappel. Y adosser au minimum un commentaire de procédure, ou la dériver d'un fichier de contenu versionné.
- **Tokens de design dupliqués** — `src/Catalog/src/styles.scss:33-70` définit deux fois les mêmes valeurs sous des noms différents : `--catalog-paper`/`--catalog-background` (`#f7fbfe`), `--catalog-slate`/`--catalog-slate-3` (`#6d8ba2`), `--catalog-blue-2`/`--catalog-blue-bright` (`#1497d6`), `--catalog-mist`/`--catalog-blue-soft` (`#9dc2da`), `--catalog-mist-2`/`--catalog-blue-pale` (`#dcecf7`). Un correctif de contraste (CAT-12) risque de n'en corriger qu'une moitié. Fusionner en une seule série et supprimer les alias.
- **Pas de `color-scheme` déclaré** — la Scanette déclare `color-scheme: light` (`src/Scan/src/styles.scss:2`), pas le Catalogue. Sur un appareil en thème sombre, les `<select>`, `<input>` et cases à cocher des filtres et de l'administration sont rendus par le navigateur en sombre sur des panneaux clairs. Ajouter `color-scheme: light` sur `:root`.
- **Téléchargement CSV fragile** — `catalog-administration-page.component.ts:747`, l'ancre n'est pas insérée dans le document avant `click()` (Firefox l'exige historiquement) et l'URL est révoquée dans un `setTimeout(0)`. Insérer l'ancre, la retirer après, et révoquer après un délai plus large.
- **Confirmations natives** — toutes les actions destructrices de l'administration (suppression de fiche, annulation de session, suppression de membre) passent par `window.confirm` (`:880`). Sur une surface entièrement dessinée par ailleurs, c'est une rupture visuelle ; pour la suppression définitive d'une fiche et d'un compte, une confirmation par saisie (retaper l'ISBN ou l'e-mail) serait plus sûre qu'un simple OK.
- **Borne absurde exposée à l'utilisateur** — `catalog-administration-page.component.ts:42`, `MAX_MIN_AGE_MONTHS = 120_000` produit le message « L'ancienneté doit être un nombre entier entre 1 et 120000 mois ». Ramener la borne à une valeur crédible (240 mois) et harmoniser l'unité avec le réglage `deadStockMinAgeDays` exprimé en jours dans les paramètres.
- **Redirection d'authentification silencieuse** — `getApiAccessToken()` peut lever `CatalogAuthenticationRedirectStartedError` ; seuls `loadWatchlist()` et `run()` (administration) la traitent. `removeItem()`, `setAlertsEnabled()`, `confirmAccountDeletion()` (compte) ainsi que `notify()` (fiche livre) et `followReference()` (recherche) affichent alors « Une erreur est survenue » juste avant que la page ne parte en redirection. Centraliser le traitement.
- **Pagination externe sans borne haute** — `catalog-search-page.component.ts:99` contient la condition dupliquée `page < 1 || !this.externalResponse || page < 1` et ne vérifie aucune borne supérieure : « Suivante » mène à une page vide affichant « Aucun autre titre ne correspond à cette recherche ».

---

# Partie 3 — API (`src/Backend/`)

Périmètre : uniquement les endpoints consommés par la Scanette et le Catalogue
(`BookController`, `BookAdministrationController`, handlers associés, `Program.cs`,
configurations EF). Le reste de l'API (bingo, produits, commandes, actualités) n'a pas
été audité.

**Point positif à conserver** — plusieurs garde-fous sont bien en place et ne doivent
pas être cassés par les correctifs : index unique filtré sur `BookMovements.ClientGestureId`
(idempotence réelle des gestes), index unique filtré sur `ScanSessions.VolunteerId`
(une seule session ouverte par bénévole), jeton de concurrence `rowversion` sur `Book`,
et vérification de propriété dans `OpenScanSessionCommandHandler`. Les routes du client
d'administration correspondent une à une aux routes serveur.

## P1 — Bloquants

### API-01 · Aucune vérification de propriété sur les endpoints de session de scan

**Symptôme.** Tout compte porteur du rôle `Tri` peut injecter des scans dans **la session d'un autre bénévole**, et clôturer n'importe quelle session à partir de son GUID. Les mouvements créés sont attribués à `session.VolunteerId`, c'est-à-dire **au bénévole victime**. La clôture forcée déclenche en plus l'envoi des alertes e-mail aux membres (`bookAlertOutbox.QueueForSessionAsync`) avant la fin réelle du tri.

**Cause.**
- `src/Backend/…/Api/Controllers/BookController.cs:411` (`POST /scan/sessions/{id}/scans`) et `:432` (`POST /scan/sessions/{id}/close`) : le `ClaimsPrincipal` n'est **même pas injecté** dans le handler d'endpoint. Aucun appel à `TryGetUserId`. La seule protection est `.RequireAuthorization("Tri")`, qui contrôle le rôle, pas l'objet.
- `ScanBookCommandHandler` (`…/Books/Commands/ScanBook/ScanBookCommandHandler.cs:97`) charge la session par identifiant et ne compare jamais `session.VolunteerId` à l'appelant.
- `CloseScanSessionCommandHandler` (`…/ScanSession/CloseScanSessionCommandHandler.cs:29`) : idem.
- La vérification existe pourtant à l'ouverture : `OpenScanSessionCommandHandler:55` renvoie `Book.ClientSessionConflict` si `existingClientSession.VolunteerId != command.VolunteerId`. Le contrôle a été oublié sur les deux endpoints suivants.

**Correctif.**
1. Injecter `ClaimsPrincipal` dans les deux endpoints et propager l'identité :
   ```csharp
   if (!TryGetUserId(principal, out var volunteerId)) return Results.Unauthorized();
   var result = await mediator.Send(new ScanBookCommand(
       ScanSessionId.Create(scanSessionId), volunteerId, request.Isbn, …), cancellationToken);
   ```
2. Dans les deux handlers, après le chargement de la session :
   ```csharp
   if (session.VolunteerId != command.VolunteerId)
   {
       return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value); // 404, pas 403 : ne pas divulguer l'existence
   }
   ```
3. Ajouter des tests de non-régression : un bénévole B ne peut ni scanner ni clôturer la session de A.

---

### API-02 · La reprise hors ligne se solde par une mise en quarantaine des gestes

**Symptôme.** Un bénévole trie hors ligne, puis retrouve le réseau. Si une session est restée `InProgress` côté serveur (appareil éteint, clôture jamais synchronisée, onglet fermé), **tous ses gestes finissent en quarantaine** et il voit « N entrées ont été mises en quarantaine après plusieurs refus serveur. Prévenez un responsable. » Les livres triés ne sont jamais publiés.

**Cause.** Enchaînement client/serveur :
1. `ScanSyncService.flushOutboxInternal()` appelle `api.openSession({clientSessionId: B})` avant d'envoyer les gestes du groupe de session B.
2. `OpenScanSessionCommandHandler:88-97` : B n'existe pas côté serveur, on passe au contrôle « une session ouverte par bénévole » ; la session A du même bénévole est `InProgress` → `Errors.Book.ActiveScanSessionExists` → **409**.
3. Côté client, `classifyFailure()` classe 4xx en `permanent`. Après `MAX_PERMANENT_ATTEMPTS = 5` tentatives, `quarantineOutboxEntry()` est appelé sur chaque geste.
4. Le job `CloseIdleScanSessionsCommand` ne libère la session A qu'après `sessionIdleTimeoutMinutes` (30 min par défaut) — plus long que les 5 tentatives, qui s'enchaînent en quelques minutes.

**Correctif.** Deux côtés, à faire ensemble :
- **Serveur** : quand une session `InProgress` du même bénévole existe et qu'un `clientSessionId` différent est demandé, ne pas répondre 409 sèchement. Soit clôturer automatiquement l'ancienne (`ScanCloseReason.Disconnect`) et ouvrir la nouvelle, soit renvoyer la session existante avec son identifiant réel pour que le client s'y rattache. La deuxième option préserve mieux le ledger.
- **Client** : dans `classifyFailure()` (`scan-sync.service.ts`), traiter le code métier `Book.ActiveScanSessionExists` comme `transient` — un conflit d'état transitoire ne doit jamais conduire à la quarantaine. Plus généralement, **ne mettre en quarantaine que sur 400/422** (donnée invalide), jamais sur 409 (conflit d'état).

---

### API-03 · Tous les scans hors ligne perdent leur horodatage réel et sont marqués suspects

**Symptôme.** Une session triée hors ligne le matin et synchronisée le soir voit **tous ses gestes réhorodatés à l'heure de synchronisation**, et **tous marqués `clockSuspect`** dans le ledger. L'historique de tri devient inexploitable, et l'indicateur de qualité de données perd tout pouvoir discriminant puisqu'il est vrai partout.

**Cause.** `ScanBookCommandHandler:316` :
```csharp
var clockSuspect = clientTimestamp < sessionStartedAt || clientTimestamp > receivedAt.Add(MaximumFutureSkew);
return clockSuspect ? (receivedAt, true) : (clientTimestamp, false);
```
Or `session.StartedAt` est fixé par `OpenScanSessionCommandHandler:41` à `dateTimeProvider.UtcNow` — c'est-à-dire **à l'instant de la synchronisation**, pas au moment où le bénévole a commencé à trier. Tous les gestes hors ligne sont donc antérieurs à `StartedAt` : la condition est systématiquement vraie.

Second défaut de la même fonction : `MaximumFutureSkew = TimeSpan.Zero` (`:27`). Une horloge d'appareil en avance ne serait-ce que d'une seconde suffit à déclarer suspect tout geste, même en ligne.

**Correctif.**
1. Faire porter au client la date de début réelle de la session (`ScanSessionSnapshot.startedAt` existe déjà côté Scanette) et l'accepter dans `OpenScanSessionCommand`, bornée : `startedAt = Min(clientStartedAt, serverNow)` et pas antérieure à, disons, 7 jours.
2. Remplacer la borne haute par une tolérance réaliste : `MaximumFutureSkew = TimeSpan.FromMinutes(2)`.
3. Ne conserver `clockSuspect` que pour ce qu'il désigne réellement — une horloge incohérente — et non pour tout travail différé.

---

### API-04 · `/catalog/search` charge tout le catalogue en mémoire à chaque requête publique

**Symptôme.** Chaque visite anonyme sur la page d'accueil ou `/catalogue` déclenche le chargement intégral de trois tables, puis un tri et une pagination en mémoire. C'est le principal moteur de charge SQL/CPU de l'API — à rapprocher du sujet coût Azure SQL déjà ouvert.

**Cause.** `…/Books/Queries/SearchCatalog/SearchCatalogQueryHandler.cs` :
- `:70` — sans terme de recherche : `books = await booksQuery.ToListAsync()`, soit **tous** les livres visibles. C'est le cas du mode « Parcourir » et des deux requêtes de la page d'accueil (`recent` et `rareOnly`).
- `:73` — `announcements = await dbContext.BookAnnouncements.AsNoTracking().ToListAsync()` : **toutes** les annonces, sans même filtrer sur `Status == Announced`.
- `:77` — `fairs = await dbContext.AssoEvents.AsNoTracking().ToListAsync()` : **tous** les événements.
- `:120-127` — genre, disponibilité, rareté, tri et `Skip/Take` sont appliqués **en LINQ-to-Objects** sur la liste complète.

**Correctif.** Faire redescendre les filtres, le tri et la pagination dans SQL. La pertinence (`Score`) et la normalisation d'accents sont la vraie difficulté : les traiter avec une colonne persistée normalisée (`TitleNormalized`, `AuthorsNormalized`, calculée à l'écriture et indexée), ou une recherche plein texte SQL Server. Dans tous les cas, remplacer les trois `ToListAsync()` par des requêtes projetées et bornées, et faire le `Count()` en SQL.

---

### API-05 · Une recherche sans résultat recharge tout le catalogue

**Symptôme.** Le pire scénario de charge est déclenché par la requête la moins utile. Sur un endpoint anonyme et sans limitation de débit (API-08), quelques requêtes `?q=xyz123` en boucle suffisent à saturer l'API et la base.

**Cause.** `SearchCatalogQueryHandler.cs:63-68` :
```csharp
books = await sqlCandidates.ToListAsync(cancellationToken);
if (books.Count == 0)
{
    books = await booksQuery.ToListAsync(cancellationToken);   // tout le catalogue
}
```
Le repli existe pour rattraper les différences de collation et les ISBN contenant des séparateurs, mais il est déclenché par **toute** recherche infructueuse, alors que le filtrage en mémoire qui suit ne rendra rien non plus dans l'immense majorité des cas.

**Correctif.** Restreindre le repli à ce qu'il vise réellement : ne le déclencher que si le terme, une fois normalisé, ressemble à un ISBN (chiffres et séparateurs), et le borner (`Take(500)`). Une fois API-04 traité avec des colonnes normalisées, le repli devient inutile et doit être supprimé.

---

### API-06 · `/books/{isbn}/metadata` : trois appels externes par scan, aucun cache, ouvert à tous

**Symptôme.** Chaque scan déclenche jusqu'à trois appels sortants (BnF SRU → OpenLibrary → Google Books), y compris pour un livre déjà présent et enrichi dans la base. L'endpoint est `AllowAnonymous` et sans limitation de débit : c'est un proxy gratuit vers ces trois services, depuis l'IP de l'association, avec un risque concret de bannissement par les fournisseurs et une latence subie par les bénévoles au pire moment.

**Cause.**
- `BookController.cs:328` — `.AllowAnonymous()`, aucune limitation.
- `…/Books/Queries/GetBookMetadata/GetBookMetadataQueryHandler.cs:16` — appelle directement `resolver.ResolveAsync(...)` : **aucune lecture préalable de la table `Books`**, aucun cache mémoire ou distribué, alors que `Book.MetadataStatus` / `MetadataSource` existent déjà et qu'une file d'enrichissement (`IBookMetadataEnrichmentQueue`) est en place.
- Côté client, `scanner.component.ts:400` appelle `getMetadata()` à **chaque** `lookup()`, même quand le catalogue local connaît déjà le titre — et le cache service worker qui devait amortir cela ne fonctionne pas (SCAN-05).

**Correctif.**
1. Lire d'abord la fiche locale : si `MetadataStatus == Resolved`, la renvoyer immédiatement ; sinon enfiler l'ISBN dans la file d'enrichissement et n'appeler le résolveur en synchrone que pour un ISBN inconnu.
2. Ajouter `AddOutputCache` (TTL 24 h, clé = ISBN) sur l'endpoint et un `Cache-Control: public, max-age=86400`.
3. Le passer derrière une politique de limitation de débit dédiée (voir API-08).
4. Côté Scanette, ne déclencher l'appel que si le catalogue local n'a pas de titre pour cet ISBN.

---

### API-07 · Le filigrane du delta perd des mises à jour (ventes hors ligne jamais propagées)

**Symptôme.** Une vente encaissée hors ligne et transmise plus tard **n'est jamais diffusée aux autres appareils**. Les stocks affichés sur les autres scanettes restent faux jusqu'à une purge complète du catalogue local.

**Cause.** Deux mécanismes qui se contredisent :
- `GetCatalogDeltaQueryHandler.cs:104-113` — le filigrane renvoyé (`nextWatermark`) est `generatedAt`, l'heure serveur, et la sélection est `book.UpdatedAt > since && book.UpdatedAt <= generatedAt`.
- `…/Domain/BookAggregate/Book.cs:112` — `RecordSale` fait `UpdatedAt = utcOccurredAt`, soit **l'heure du geste**, pas l'heure d'enregistrement. Idem `RecordAnnouncementEntry:98` et `RecordRejection:365`.
- `RegisterSaleCommandHandler.cs:174-182` conserve intentionnellement l'horodatage passé d'un geste hors ligne (contrairement aux scans, cf. API-03).

Une vente de 10 h transmise à 18 h écrit donc `UpdatedAt = 10 h`, alors que les appareils ont déjà un filigrane à 12 h : le livre ne ressortira **dans aucun delta ultérieur**.

Le même mécanisme fait perdre toute écriture dont la transaction est committée après le `generatedAt` d'un delta déjà servi.

**Correctif.** Découpler l'horodatage métier du filigrane technique :
- ajouter une colonne technique `RowVersion` (déjà présente sur `Book` !) ou `LastWrittenAt` positionnée par un intercepteur `SaveChanges` à l'heure serveur, et **paginer le delta là-dessus**, en gardant `UpdatedAt` pour l'affichage métier ;
- ou, en correctif immédiat et peu coûteux, reculer le filigrane servi d'une marge (`generatedAt - 5 min`) et accepter le recouvrement — le client déduplique déjà par ISBN dans `applyCatalogDelta`.

---

### API-08 · Limitation de débit quasi inexistante sur des endpoints anonymes coûteux

**Symptôme.** Aucune protection contre l'abus, accidentel (crawler, SSR en boucle) ou délibéré.

**Cause.**
- `…/Api/Common/RateLimiting/RateLimiting.cs` ne définit **qu'une** politique, `Login` (3 requêtes / 10 s), et aucun `GlobalLimiter`.
- Un seul `RequireRateLimiting` dans tout le backend : `AuthenticationController.cs:53`.
- Les endpoints anonymes les plus coûteux — `/catalog/search` (API-04, API-05), `/books/{isbn}/metadata` (API-06), `/catalog/sitemap.xml` (énumère tout le catalogue) — n'ont **aucune** limite.

**Correctif.** Déclarer un `GlobalLimiter` par IP (fenêtre glissante, ~100 req/min) et une politique plus stricte pour les endpoints coûteux (`Public` ~20 req/min), puis appliquer `.RequireRateLimiting("Public")` sur `/catalog/search`, `/books/{isbn13}/metadata`, `/catalog/reference/search` et `/catalog/sitemap.xml`.

---

## P2 — Bugs et défauts notables

### API-09 · Le verdict serveur ne peut jamais valoir « Wanted »

`ScanBookCommandHandler.cs:299` — `CalculateVerdict` passe `ActiveRequesterCount: 0` en dur à `BookVerdictCalculator`. Le verdict renvoyé au scanner après synchronisation, et journalisé dans le mouvement, **ne peut donc jamais être `Wanted`**, même pour un titre suivi par des membres — alors que le delta transporte bien le drapeau `isWanted` (`GetCatalogDeltaQueryHandler.cs:93`) et que le client s'en sert pour son verdict local. Le verdict local et le verdict serveur divergent sur exactement le cas qui compte.
→ **Correctif :** compter les items de liste de suivi actifs correspondant à l'ISBN (ou à son `WorkId`) et alimenter `ActiveRequesterCount`. À traiter avec SCAN-10 et SCAN-11 : les trois portent sur la même règle.

### API-10 · Le delta catalogue est en O(livres × annonces) et sans pagination

`GetCatalogDeltaQueryHandler.cs` :
- `:43` — **tous** les `AssoEvents` non annulés sont chargés, puis filtrés en mémoire pour n'en garder qu'un seul (le prochain).
- `:63-70` — **tous** les items de liste de suivi de tous les membres actifs.
- `:71` — **toutes** les annonces `Announced`, sans restriction aux livres du delta.
- `:82-97` — pour **chaque** livre projeté : `announcements.Where(...)` parcourt toutes les annonces et `activeWatchlistItems.Any(...)` parcourt tous les items. Avec 20 000 livres et 2 000 suivis, cela représente des dizaines de millions de comparaisons en mémoire **par appel**.
- `:120-152` — un simple ajout d'un membre à sa liste de recherche met `watchlistStateChanged` à vrai et déclenche le rechargement de **tout le catalogue** (`allBooks`) pour tous les appareils.
- Le premier appel (`since == null`) renvoie l'intégralité du catalogue **sans pagination ni compression déclarée**.

La Scanette synchronise automatiquement toutes les 60 s (`scanner.component.ts:165`) et plusieurs postes tournent simultanément en bourse.
→ **Correctif :** indexer les annonces dans un `Dictionary<Isbn13, int>` et les suivis dans deux `HashSet` (ISBN et WorkId) avant la projection ; restreindre les requêtes aux ISBN du delta ; ajouter une pagination (`maxItems` + curseur) sur le premier chargement ; et ne recalculer `isWanted` que pour les livres réellement concernés au lieu de rejouer tout le catalogue.

### API-11 · Aucune reprise sur conflit de concurrence

`Book` porte un jeton `rowversion` (`BookConfiguration.cs:97-98`), ce qui est la bonne pratique — mais aucun handler ne rattrape `DbUpdateConcurrencyException`. Deux postes qui scannent le même ISBN au même instant (courant en bourse) produisent une **500**, que le client classe `transient` et réessaie avec un backoff pouvant atteindre 15 minutes (`scan-sync.service.ts`, `RETRY_BACKOFF_MAX_MS`).
→ **Correctif :** encadrer la transaction de `ScanBookCommandHandler` et `RegisterSaleCommandHandler` d'une boucle de reprise (3 tentatives, rechargement de l'entité entre chaque). Une stratégie d'exécution EF (`EnableRetryOnFailure`) ne suffit pas ici : elle ne couvre pas les conflits optimistes.

### API-12 · CORS en « fail-open » et liste committée désynchronisée

`Program.cs:31-38` : si `Cors:AllowedOrigins` est vide ou absent, la politique retombe sur `AllowAnyOrigin()` avec `AllowAnyHeader` et `AllowAnyMethod`. Un oubli de configuration ouvre l'API à toutes les origines **silencieusement**.

Par ailleurs, `src/Backend/…/appsettings.json:25-30` liste quatre origines et **omet `https://scan.volepapillondamour.fr`**. Le déploiement corrige la liste par variables d'environnement (`infra/parameters/main.dev.bicepparam:211-218` la contient bien, avec le FQDN ACA de secours), donc la production fonctionne — mais toute exécution s'appuyant sur les valeurs committées bloque la Scanette, et la dérive entre les deux sources est un piège.
→ **Correctif :** échouer au démarrage en Production si la liste est vide, plutôt que d'ouvrir ; et aligner `appsettings.json` sur la liste réelle (ou l'y retirer complètement pour que l'infrastructure reste la seule source de vérité).

### API-13 · Les jokers SQL ne sont pas échappés dans la recherche

`SearchCatalogQueryHandler.cs:57` — `var pattern = $"%{term}%";` puis `EF.Functions.Like(...)`. Un terme contenant `%`, `_` ou `[` est interprété comme un motif : `?q=%` remonte tout le catalogue, `?q=%a%b%c%d%` force des balayages coûteux. Pas d'injection SQL (la requête reste paramétrée), mais amplification de charge et résultats incohérents pour l'utilisateur.
→ **Correctif :** échapper `%`, `_`, `[` et `\` dans le terme et passer un caractère d'échappement (`EF.Functions.Like(champ, motif, "\\")`).

### API-14 · La prochaine bourse disparaît de la Scanette le jour même

`GetCatalogDeltaQueryHandler.cs:48-50` — le prochain événement est retenu si `(DateEnd ?? DateStart) > new DateTimeOffset(generatedAt, TimeSpan.Zero)`, donc comparé à l'instant **UTC**. Une bourse dont la date de fin est stockée à minuit expire à 00:00 UTC, soit **01:00 ou 02:00 heure de Paris le jour même de l'événement** : les scanettes perdent le libellé « Prochaine bourse » et l'affichage retombe sur « date à préciser » pendant la bourse.
→ **Correctif :** comparer à la fin de la journée locale (`DateEnd` + 1 jour en Europe/Paris). Même famille de défaut que CAT-04 et CAT-15 : à traiter avec l'utilitaire de fuseau commun.

### API-15 · Table complète chargée sur le chemin chaud d'un encaissement

`RegisterSaleCommandHandler.cs:100` — `var fairs = await dbContext.AssoEvents.ToListAsync(cancellationToken);` pour résoudre la bourse correspondant à la vente. Toute la table à chaque ligne de caisse validée, alors qu'une seule bourse encadre l'horodatage.
→ **Correctif :** filtrer en SQL sur la fenêtre de dates du geste (`DateStart <= occurredAt && (DateEnd ?? DateStart) >= occurredAt`), avec un index adapté.

## P3 — Qualité et exploitation

### API-16 · Divers

- **La configuration JSON ne s'applique pas aux endpoints réellement utilisés.** `Program.cs:41-47` configure `AddControllers().AddJsonOptions(...)` (options **MVC**), alors que 100 % des routes sont des **Minimal APIs**, qui lisent `Microsoft.AspNetCore.Http.Json.JsonOptions`. Le `camelCase` fonctionne par coïncidence (`JsonSerializerDefaults.Web` l'applique déjà), mais toute personnalisation future sera silencieusement ignorée. Remplacer par `builder.Services.ConfigureHttpJsonOptions(...)`.
- **`WriteIndented = true` en production.** Même ligne. Si l'option finit par s'appliquer (correctif ci-dessus), elle ajoutera 15 à 30 % de volume à **toutes** les réponses, y compris le delta catalogue téléchargé sur le réseau mobile d'une salle des fêtes. La conditionner à `builder.Environment.IsDevelopment()`.
- **Aucun cache HTTP sur les endpoints publics.** `/catalog/search`, `/catalog/books/{isbn}`, `/catalog/works/{id}`, `/catalog/fairs/next` et `/catalog/sitemap.xml` sont anonymes, identiques pour tous les visiteurs et parfaitement cacheables, mais ne renvoient aucun `Cache-Control` et ne passent par aucun `OutputCache`. Le SSR du catalogue refait donc l'intégralité du travail à chaque visite et à chaque passage de crawler.
- **La liste des genres ne décrit pas le catalogue.** `SearchCatalogQueryHandler.cs:79-86` calcule `genres` à partir de `projected`, c'est-à-dire du **sous-ensemble déjà filtré par la recherche**. Sur la page d'accueil, la liste déroulante « Genre » est donc dérivée d'une requête `pageSize: 4` : elle ne reflète pas les genres réellement présents. Le client masque le problème en fusionnant avec une liste figée (`catalog-genres.ts`), mais l'API ne tient pas son contrat. Calculer les genres sur le catalogue visible complet (requête `Distinct` dédiée, mise en cache).
- **Message d'erreur trompeur sur `availability`.** `BookController.cs:56-60` annonce « availability must be one of all, available, or next-fair », alors que `TryParseAvailability` (`:710`) accepte six valeurs — dont `next`, celle que le client envoie réellement. Par ailleurs la liste des littéraux acceptés est **dupliquée** entre le `switch` et le `return` (`:714-726`, même schéma dans `TryParseSort` `:729-742`) : ajouter une valeur au `switch` sans l'ajouter au `return` la fera rejeter silencieusement. Dériver la validation d'une seule source.
- **`/asso-events` dépend du fuseau du serveur.** `EventRepository.GetNextEventsAsync` (`…/Persistence/Repositories/EventRepository.cs:68`) filtre sur `DateTimeOffset.Now.Date`, donc sur l'heure locale du conteneur — UTC en production. Entre minuit et 02:00 heure de Paris, la notion de « aujourd'hui » diverge d'un jour. Utiliser explicitement `Europe/Paris` (même utilitaire que API-14, CAT-04 et CAT-15).

---

# Récapitulatif

| ID | Sévérité | Titre | Fichier principal |
|---|---|---|---|
| SCAN-01 | P1 | Caméra morte après un scan en échec | `scanner.component.ts:1157` |
| SCAN-02 | P1 | Message « Saisissez un ISBN » après scan caméra | `scanner.component.ts:372` |
| SCAN-03 | P1 | Changement d'utilisateur sans purge locale | `scanner.component.ts:596` |
| SCAN-04 | P1 | Bandeau dégradé qui casse la mise en page | `styles.scss:62` |
| SCAN-05 | P1 | Cache hors ligne des notices inopérant | `ngsw-config.json` |
| SCAN-06 | P2 | Douchette ignorée après un clic | `scanner.component.ts:1255` |
| SCAN-07 | P2 | Caméra qui recouvre l'état de synchro | `styles.scss:1674` |
| SCAN-08 | P2 | Verdict clipé sur petit écran | `styles.scss:524, 789` |
| SCAN-09 | P2 | Titre écrasé par une notice partielle | `scan-workflow.service.ts:354` |
| SCAN-10 | P2 | « Recherché par » toujours à 1 | `scan-verdict.service.ts:26` |
| SCAN-11 | P2 | Verdict « TooMany » inatteignable | `scan-verdict.service.ts:33` |
| SCAN-12 | P2 | Échec de connexion silencieux | `scanner.component.ts:590` |
| SCAN-13 | P2 | Requêtes réseau sans délai maximal | `scan-api.service.ts` |
| SCAN-14 | P3 | Pas de retour à l'accueil depuis Tri | `scanner.component.html:157` |
| SCAN-15 | P3 | Bouton « Photo » non accessible au clavier | `scanner.component.html:262` |
| SCAN-16 | P3 | `aria-labelledby` orphelin | `scanner.component.html:145` |
| SCAN-17 | P3 | Panneau de synchro hors charte | `scanner.component.scss` |
| SCAN-18 | P3 | Contrastes WCAG insuffisants | `styles.scss:1-14` |
| SCAN-19 | P3 | Manifeste PWA / iOS incomplets | `manifest.webmanifest`, `index.html` |
| SCAN-20 | P3 | Divers (cache, caméra, duplication) | plusieurs |
| CAT-01 | P1 | Google Maps sans consentement | `catalog-home-page.component.html:56` |
| CAT-02 | P1 | Crash si `localStorage` inaccessible | `cookie-consent.service.ts:73` |
| CAT-03 | P2 | Une seule date sur « Les prochaines dates » | `catalog-home-page.component.ts:159` |
| CAT-04 | P2 | Fichier `.ics` à la mauvaise heure | `catalog-calendar.ts:36` |
| CAT-05 | P2 | Titre d'onglet persistant | `catalog-book-detail-page.component.ts:160` |
| CAT-06 | P2 | Résidus SEO dans le `<head>` | `catalog-book-detail-page.component.ts:167` |
| CAT-07 | P2 | Soft 404 sur toute URL inconnue | `app-routing.module.ts:47` |
| CAT-08 | P2 | Résultats périmés / pas d'état de chargement | `catalog-search-page.component.ts:168` |
| CAT-09 | P2 | Lien Administration masqué aux admins | `catalog-auth.service.ts:180` |
| CAT-10 | P2 | Menu compte non fermable ni navigable | `catalog-navigation.component.ts:112` |
| CAT-11 | P2 | Filtres qui naviguent à chaque flèche | `catalog-search-page.component.html:37` |
| CAT-12 | P2 | Contrastes WCAG insuffisants | `styles.scss:33` |
| CAT-13 | P2 | Colonne « Vendus » vide | `catalog-administration-page.component.html:188` |
| CAT-14 | P2 | Message de succès persistant | `catalog-administration-page.component.ts:869` |
| CAT-15 | P2 | Filtre de dates décalé d'un jour | `catalog-administration-page.component.ts:517` |
| CAT-16 | P3 | Textes de remplissage en production | `catalog-administration-page.component.html:18` |
| CAT-17 | P3 | Libellé accessible « Le Du … au … » | `catalog-home-page.component.html:15` |
| CAT-18 | P3 | Divers (tokens, liens, CSV, confirmations) | plusieurs |
| API-01 | P1 | Pas de contrôle de propriété de session (BOLA) | `BookController.cs:411, 432` |
| API-02 | P1 | Reprise hors ligne → mise en quarantaine des gestes | `OpenScanSessionCommandHandler.cs:88` |
| API-03 | P1 | Horodatage hors ligne écrasé et marqué suspect | `ScanBookCommandHandler.cs:316` |
| API-04 | P1 | `/catalog/search` charge tout en mémoire | `SearchCatalogQueryHandler.cs:70` |
| API-05 | P1 | Recherche vide → rechargement du catalogue entier | `SearchCatalogQueryHandler.cs:63` |
| API-06 | P1 | Metadata : 3 appels externes/scan, sans cache, anonyme | `GetBookMetadataQueryHandler.cs:16` |
| API-07 | P1 | Filigrane du delta : ventes hors ligne jamais propagées | `GetCatalogDeltaQueryHandler.cs:104` |
| API-08 | P1 | Limitation de débit quasi inexistante | `RateLimiting.cs:9` |
| API-09 | P2 | Verdict serveur jamais « Wanted » | `ScanBookCommandHandler.cs:299` |
| API-10 | P2 | Delta en O(livres × annonces), sans pagination | `GetCatalogDeltaQueryHandler.cs:82` |
| API-11 | P2 | Aucune reprise sur conflit de concurrence | `ScanBookCommandHandler.cs` |
| API-12 | P2 | CORS fail-open + liste committée désynchronisée | `Program.cs:31` |
| API-13 | P2 | Jokers SQL non échappés dans la recherche | `SearchCatalogQueryHandler.cs:57` |
| API-14 | P2 | Prochaine bourse expirée en UTC le jour même | `GetCatalogDeltaQueryHandler.cs:48` |
| API-15 | P2 | Table complète chargée à chaque encaissement | `RegisterSaleCommandHandler.cs:100` |
| API-16 | P3 | Divers (options JSON, cache HTTP, genres, fuseaux) | plusieurs |

## Limites de cet audit

- Analyse **statique** uniquement : aucune campagne de tests, aucune exécution des suites `ng test` ou `dotnet test`, aucune mesure de charge. Chaque correctif doit être validé sur appareil réel — en particulier SCAN-04, SCAN-07 et SCAN-08, qui dépendent du rendu, et API-04/API-10, dont l'impact réel dépend du volume actuel du catalogue.
- **Périmètre backend limité** aux endpoints consommés par la Scanette et le Catalogue : `BookController`, `BookAdministrationController`, les handlers `Books`/`Watchlist`/`ScanSession` associés, `Program.cs` et les configurations EF correspondantes. Le reste de l'API (bingo, produits, commandes, actualités, authentification héritée) n'a pas été audité, ni le worker, ni les modèles de données historiques.
- Les scénarios de charge cités (nombre de livres, de suivis, d'annonces) sont des ordres de grandeur illustratifs, pas des mesures. Avant de dimensionner les correctifs API-04 et API-10, relever les volumes réels en base.
- Les ratios de contraste ont été calculés sur les valeurs de la palette. Certains usages réels peuvent superposer un fond légèrement différent ; à revérifier au cas par cas avec l'inspecteur du navigateur.
- Les correctifs proposés pour SCAN-11 / API-09 (règle de verdict) et CAT-03 (nombre de dates affichées) touchent au métier : à arbitrer avec l'association avant implémentation.
- API-12 mérite une vérification en environnement déployé : la liste CORS réellement appliquée provient des variables d'environnement de l'infrastructure, pas du fichier committé. Confirmer par `az containerapp show` avant de conclure.
