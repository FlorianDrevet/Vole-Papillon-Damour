# PWA de scan et de tri

Le scanner Angular couvre maintenant `S0-2` et la tranche locale `P1-5`. Il accepte un
ISBN saisi au clavier, envoyé par une scanette USB, ou lu par la caméra du navigateur,
puis sépare strictement deux flux :

- le verdict, calculé immédiatement depuis la copie locale du catalogue ;
- la notice bibliographique, récupérée en tâche de fond par `GET /books/{isbn13}/metadata`.

Les trois magasins IndexedDB (`catalog`, `outbox`, `session`) conservent la copie de
travail, les décisions et la session active. Une décision `Kept` ou `Rejected` est
transmise séquentiellement avec son `ClientGestureId` dès que le réseau et un compte
bénévole portant le rôle Entra `Tri` sont disponibles. Les gestes `Pending` survivent à
la fermeture et sont restaurés au prochain lancement ; aucune donnée d'outbox n'est
supprimée par une purge du catalogue.

La connexion Entra est optionnelle pour le mode local : le bouton de connexion active
la synchronisation du catalogue delta et la vidange de la file. Le service worker
Angular met en cache la coquille et les notices bibliographiques, sans mélanger le
cache navigateur avec IndexedDB.

La caméra utilise `@zxing/browser` avec le décodeur ZXing en mode de recherche renforcé
(`TRY_HARDER`) pour les codes 1D. Elle accepte les EAN-13/EAN-8 des livres, ainsi que les
QR codes dont le contenu est un ISBN, et fonctionne dans Safari iOS lorsqu'elle est
ouverte sur une URL HTTPS. Une photo peut aussi être sélectionnée depuis l'iPhone si la
caméra continue n'est pas disponible.

Pour une photo, le décodeur essaie également des recadrages, une réduction de taille et
un seuillage noir/blanc afin de mieux tolérer les prises de vue difficiles. Une photo d'un
écran avec moirage ou reflets peut toutefois rester illisible ; une photo nette du code
EAN imprimé est le cas de référence.

## Lancer en local

Depuis `src/Scan` :

```bash
npm ci
npm start -- --port 4300
```

Le port `4300` correspond à l'URI SPA locale déclarée par `infra/entra/Configure-EntraApps.ps1`.
Sans connexion Entra, la saisie et le tri local restent utilisables ; la synchronisation
protégée attend un compte doté de `Tri`.

Le lancement de l'AppHost est recommandé pour démarrer l'API et la sonde ensemble :

```bash
dotnet run --project ../Backend/Vole_Papillon_Damour.AppHost
```

Le mode développement reste disponible sur `http://<IP-DU-PORTABLE>:4202` pour la
saisie clavier et les essais LAN. L'accès caméra d'un iPhone exige toutefois un
contexte sécurisé : pour un essai réel sur téléphone, utiliser l'URL publique HTTPS
produite par le workflow `Scan - deploy`, sans tunnel réseau.

## Déployer sur Azure

Le workflow manuel `.github/workflows/scan-deploy.yml` construit l'image avec l'URL
publique de l'API, la pousse dans `vpdacrdev`, puis met à jour `vpd-scan-ca-dev`. Son
résumé GitHub fournit l'URL HTTPS à ouvrir dans Safari. L'infrastructure est créée par
`Infra - deploy` et le worker par `Worker - deploy`.

Ordre du premier déploiement : `Infra - deploy` en `what-if`, `Infra - deploy` en
`deploy`, `API - deploy` avec migration, puis `Scan - deploy` et `Worker - deploy`.

## Vérifications

```bash
npm test -- --watch=false --browsers=ChromeHeadless
npm run build
```

Le build de production génère `ngsw-worker.js`, `ngsw.json` et le manifeste PWA. Le
service worker est désactivé en développement afin de ne pas conserver un bundle obsolète
pendant les essais.
