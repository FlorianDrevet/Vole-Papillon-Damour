# Sonde de scan

La sonde `S0-2` est une application Angular en consultation seule. Elle accepte un
ISBN saisi au clavier, envoyé par une scanette USB, ou lu par la caméra du navigateur,
puis interroge `GET /books/{isbn13}/metadata` sur l'API. Elle n'enregistre ni session,
ni file, ni donnée en base.

La caméra utilise `@zxing/browser` avec le décodeur ZXing en mode de recherche renforcé
(`TRY_HARDER`) pour les codes 1D. Elle accepte les EAN-13/EAN-8 des livres, ainsi que les
QR codes dont le contenu est un ISBN, et fonctionne dans Safari iOS lorsqu'elle est
ouverte sur une URL HTTPS. Une photo peut aussi être sélectionnée depuis l'iPhone si la
caméra continue n'est pas disponible.

## Lancer en local

Depuis `src/Scan` :

```bash
npm ci
npm start
```

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
