# Journal de décision — reprise nocturne des 4–5 septembre 2026

Ce journal complète `NEXT.md` pour les arbitrages pris pendant la reprise. Il ne contient
aucun secret, jeton ni mot de passe.

## État livré

- La PR #56 est fusionnée (`ae75d772`) : compte catalogue, watchlist, suppression de compte,
  alertes différées et administration du stock mort sont présents.
- La PR #58 est fusionnée (`6a5a736`, changement source `4453315`) : une notice BnF reste
  l'autorité bibliographique ; si elle ne fournit pas de `WorkId`, Open Library enrichit ce
  seul champ. Si Open Library échoue, la notice BnF complète est conservée. Le runtime API /
  Worker a été roulé par `Books runtime - deploy` `33924236821`, sans migration car le schéma
  était déjà à jour.
- La PR #57 est fusionnée (`64c347e`) : la carte MSAL Scan protège `/scan/*`, ce qui couvre
  les routes imbriquées `/scan/catalog/delta` et `/scan/sessions`. `Scan - deploy`
  `33924618301` est réussi.
- La PR #59 est fusionnée (`dfd8e69`) : les erreurs transitoires d'enrichissement sont
  horodatées, mises en cooldown une heure et ordonnées pour que les livres jamais tentés ne
  soient pas bloqués par une ligne fournisseur défaillante. `Books runtime - deploy`
  `33926622823` est réussi sans migration.
- La PR #61 est fusionnée (`dcc0c23`) : le lookup des suppressions en attente reste
  provider-neutral dans SQLite/Aspire, et la finalisation retire les données strictement
  membres (watchlist, items, historique d'alertes, rebonds et outbox `AlertEmail`) avant la
  suppression ou l'anonymisation locale. Les mouvements et sessions historiques sont
  conservés selon la décision de rétention. Les deux contrôles CI `33929259723` et
  `33929261525` sont verts ; `Books runtime - deploy` `33929828651` a roulé API et Worker
  avec le tag partagé `dcc0c23`, sans migration.
- Le smoke externe du 2026-09-05 confirme 200 pour le catalogue, la Scanette et l'API, 401
  pour la watchlist sans jeton, les en-têtes `noindex` du compte et de l'administration, et
  une metadata ISBN BnF avec `WorkId=OL10263W`.
- La PR #63 est fusionnée (`3a6e887`) : la meta robots statique du catalogue est privée par
  défaut et `AppComponent` recalcule `index, follow` ou `noindex, nofollow` selon la route.
  `Catalog - deploy` `33932087193` a roulé `vpd-catalog:3a6e887`; le smoke HTTPS confirme
  les deux signaux privés et le signal public, ainsi que `robots.txt` et `sitemap.xml` en 200.

## Décisions prises

### Enrichissement bibliographique

J'ai retenu l'enrichissement minimal plutôt qu'un remplacement de source : la BnF reste
responsable du titre, des auteurs, de l'éditeur et de l'année ; Open Library ne fournit que
la clé d'œuvre manquante. Cela évite de dégrader les notices françaises tout en rendant les
watchlists « œuvre » fonctionnelles. Une seule requête Open Library supplémentaire est faite
par livre lorsque la BnF n'a pas de `WorkId`. Le repli en cas d'indisponibilité est testé.

Le rapprochement titre+auteur normalisés n'est pas activé préventivement : il peut créer des
faux positifs pour les séries, homonymes et adaptations. Il sera décidé après le relevé
`QT-01`, conformément au plan.

J'ai conservé le budget `ResolveAttempts` pour les seuls résultats réellement `NotFound`.
Une panne fournisseur ou de couverture garde l'état `Pending`/`NotFound`, écrit
`LastAttemptAt`, puis est rééligible après une heure pour un `Pending` (ou selon sa fenêtre
`NotFound`). L'ordre met les lignes jamais tentées en tête et le test de lot unitaire vérifie
qu'une première panne ne retarde pas le livre suivant.

### Déploiements

- Les migrations EF ont été appliquées une fois, avec le runtime Books `33922677695` et
  `run_migrations=true`. Les rollouts applicatifs suivants utilisent `run_migrations=false`
  lorsqu'aucune migration n'est présente.
- API et Worker sont toujours construits depuis le même commit et partagent le même tag ;
  cette contrainte est conservée pour éviter un décalage de modèle entre les deux hôtes.
- Le déploiement final `33929828651` utilise `vpd-api:dcc0c23` et `vpd-worker:dcc0c23`,
  avec `run_migrations=false` parce que les migrations étaient déjà appliquées par
  `33922677695`. Le job confirme le rollout des deux Container Apps.
- Aucun changement DNS n'a été réécrit après vérification : CNAME, TXT `asuid` et certificats
  managés SNI des domaines `livres` et `scan` sont déjà corrects.

### Revue — suppression et anonymisation de compte

La revue de la PR #61 a fait apparaître un défaut plus important que le seul échec SQLite :
`FinalizeAsync` anonymisait ou supprimait `User`, mais pouvait laisser les projections
strictement membre rattachées à son identifiant interne. Cela contredisait les décisions
`01-decisions.md` et `02-modele-de-donnees.md`. Le correctif charge et supprime, dans la même
transaction, les `WatchlistItems`, `Watchlist`, `UserAlertHistory`, `EmailBounceEvents` et
les messages d'outbox `AlertEmail` du membre. Les `BookMovements`, `ScanSessions` et autres
éléments d'audit nécessaires restent intacts quand des mouvements sont retenus ; sans
mouvement, l'utilisateur est supprimé.

Deux tests d'infrastructure couvrent maintenant les branches « mouvements retenus » et
« aucun mouvement », en plus de la régression SQLite. La suite locale compte 291 tests
backend. Cette décision est volontairement limitée au membre local : l'appel Graph reste
effectué avant la finalisation locale et les demandes échouées restent rejouables par
l'outbox.

### Entra et connexion

- Les URI publiques de `vpd-catalog-dev` et `vpd-scan-dev` sont présentes et vérifiées.
- L'URI publique du catalogue est suffisante pour le déploiement. L'URI locale catalogue
  `http://localhost:4203` n'a pas été sauvegardée pendant cette session : sa création dans
  Entra est une écriture interactive et aurait nécessité une confirmation au moment du clic.
  Recommandation : l'ajouter lors d'une prochaine session de développement local, puis
  vérifier le redirect sur le port 4203.
- Aucun identifiant ni consentement OAuth n'a été automatisé pendant l'absence de l'utilisateur.
  Le bouton du catalogue a seulement été déclenché depuis `/compte` dans un profil Chrome déjà
  authentifié ; le redirect revient correctement sur le compte et n'a modifié aucune donnée.
  Le correctif Scan a été validé par test d'intercepteur et déployé ; le parcours avec un
  compte `Tri` reste une vérification humaine.

### Messagerie ACS

Le code de livraison et de rebond est en place, mais l'envoi reste désactivé. Le portail ACS
affiche encore `Verification is underway` pour `mail.volepapillondamour.fr`. Ne pas activer
l'expéditeur ni déclarer le test d'e-mail réussi avant l'état `Verified` et un cycle complet
réception/boîte indésirable.

## Revue et points ouverts

1. Les mesures `QT-02` du nouveau `Sweep`/`Enrich`, `P1-9` sur quelques milliers puis
   15 000 fiches, `P1-10` hors ligne et `P1-11` sur l'échantillon physique ne sont pas
   fabriquées à partir de smoke tests HTTP ; elles restent à faire avec leurs protocoles.
2. Le défaut SEO relevé en revue est résolu par la PR #63 : le HTML statique commence en
   `noindex, nofollow`, puis la directive est recalculée par route. Le smoke live confirme
   `index, follow` sur `/` et `noindex, nofollow` dans le HTML et l'en-tête sur `/compte` et
   `/administration`.
3. Les avertissements existants restent visibles : vulnérabilités NuGet signalées par
   `NU1903`, dépréciation `WithOpenApi`, avertissements nullable/legacy, dépréciation Node 20
   dans les actions GitHub et avertissement de taille de bundle Angular. Ils ne sont pas
   introduits par cette reprise.
4. L'envoi ACS est désactivé, donc aucune course alerte/suppression n'a été observée en
   production. Comme le sweep traite encore la livraison des alertes avant la file de
   suppression, il faudra, avant d'activer ACS, décider si les suppressions en attente
   doivent être prioritaires ou si les listes doivent être neutralisées dès la demande.
   Le correctif de nettoyage rend la finalisation sûre, mais ne remplace pas cette décision
   de séquencement.

## Checklist recommandée demain matin

1. Les deux contrôles CI de la PR #63 (`33931556397`, `33931558967`) et le déploiement
   Catalogue `33932087193` sont terminés avec succès ; vérifier le prochain CI `main` si
   GitHub en programme un après la fusion documentaire.
2. Depuis le navigateur, tester `/compte` avec le compte Entra, ajouter une œuvre depuis une
   fiche, constater sa présence dans la watchlist puis tester le retrait. Vérifier séparément
   la demande de suppression avec un compte de test.
3. Sur `https://scan.volepapillondamour.fr`, faire le login d'un compte ayant `Tri`, ouvrir
   une session et confirmer dans le réseau que les appels imbriqués portent
   `Authorization: Bearer`.
4. Relever dans Application Insights les traces `Worker sweep completed` et
   `Worker enrichment completed`; vérifier aussi qu'une erreur fournisseur renseigne
   `LastAttemptAt` et ne bloque pas les candidats suivants, puis lancer la campagne de deux
   heures si la périodicité n'est pas encore démontrée.
5. Relever l'état ACS. Tant qu'il n'est pas `Verified`, laisser l'envoi désactivé.
6. Exécuter et consigner les protocoles physiques `P1-9`, `P1-10` et `P1-11` dans `NEXT.md`.
