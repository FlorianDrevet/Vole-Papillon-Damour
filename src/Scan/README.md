# PWA de scan et de tri

Le scanner Angular couvre maintenant `S0-2` et la tranche locale `P1-5`. Il accepte un
ISBN saisi au clavier, envoyé par une scanette USB, ou lu par la caméra du navigateur,
puis sépare strictement deux flux :

- le verdict, calculé immédiatement depuis la copie locale du catalogue ;
- la notice bibliographique, récupérée en tâche de fond par `GET /books/{isbn13}/metadata`.

Les quatre magasins IndexedDB (`catalog`, `outbox`, `sales`, `session`) conservent la copie
de travail, les décisions, les ventes locales et la session active. Une décision `Kept` ou
`Rejected` est transmise séquentiellement avec son `ClientGestureId` dès que le réseau et
un bénévole portant le rôle Entra `Tri` sont disponibles. Une validation de caisse crée
également une vente durable avec son propre `ClientGestureId`, décrémente immédiatement la
projection locale et la rejoue vers `POST /scan/sales` dès que le réseau et le rôle `Caisse`
sont disponibles. Une session terminée demande aussi sa clôture serveur ; cette demande
reste dans IndexedDB jusqu'à confirmation, afin que les livres soient publiés même après
une coupure réseau. Les gestes et ventes non transmis survivent à la fermeture et sont
restaurés au prochain lancement ; aucune donnée d'outbox n'est supprimée par une purge du
catalogue.

L'accès à l'application est protégé par Entra : l'écran de connexion est affiché tant
qu'aucun compte n'est ouvert, et un compte portant le rôle `Tri` ou `Caisse` peut atteindre
le scan. Les actions d'accueil restent filtrées : `Tri` ouvre le tri, `Caisse` ouvre la
vente, et les deux rôles peuvent consulter le catalogue. Une perte de session ou un échec
de renouvellement du jeton renvoie également vers cet écran. Les environnements déclarent
l'autorité CIAM avec le chemin du tenant ; le service de connexion fournit aussi
explicitement la page de retour de l'application et affiche l'échec de démarrage au lieu de
l'ignorer. Le service worker Angular met en cache la coquille et les notices
bibliographiques, sans mélanger le cache navigateur avec IndexedDB.

La caméra utilise `@zxing/browser` avec le décodeur ZXing en mode de recherche renforcé
(`TRY_HARDER`) pour les codes 1D. Sur l'écran de tri, elle démarre automatiquement à
l'arrivée dans la vue de scan ; le bouton d'activation n'est donc plus nécessaire. Elle
accepte les EAN-13/EAN-8 des livres, ainsi que les QR codes dont le contenu est un ISBN,
et fonctionne dans Safari iOS lorsqu'elle est ouverte sur une URL HTTPS. Une photo peut
aussi être sélectionnée depuis l'iPhone si la caméra continue n'est pas disponible.

Les écrans `Caisse` et `Consultation` ouvrent eux aussi la caméra dès leur arrivée et
conservent le même flux autorisé entre deux lectures pour enchaîner les livres sans
redemander l’autorisation. Après une détection, le flux reste ouvert mais la détection est
mise en pause pendant l’affichage du résultat ; elle reprend dès que nécessaire. La caisse
affiche les sorties en cours sous le cadre caméra et chaque ligne peut être retirée
individuellement. En tri, le même ISBN relu en moins de cinq secondes dans la session
affiche « déjà scanné à
l'instant » sans bloquer la décision (`RG-04`). La fin d'une session vide le snapshot
local uniquement après synchronisation et clôture de la session distante ; en cas
d'échec réseau, les gestes restent conservés localement.

La réponse à la demande d'autorisation de caméra est conservée par le navigateur, pas
par l'application. Pendant une session, l'application conserve toutefois le flux déjà
autorisé au lieu de rappeler `getUserMedia()` après chaque livre. Pour éviter une nouvelle
demande à chaque visite, utiliser toujours
la même origine HTTPS (même protocole, hôte et port), hors navigation privée, et vérifier
le réglage Caméra du site dans Safari ou le navigateur utilisé. Le cache MSAL en
`localStorage` conserve la session Entra, mais ne peut pas mémoriser cette permission.

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
En production, l'origine canonique est `https://scan.volepapillondamour.fr` ; le FQDN
technique ACA reste une adresse de secours. Un compte Entra doté du rôle `Tri` ou `Caisse`
est nécessaire pour passer l'écran de connexion et ouvrir l'application.

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
publique de l'API et l'origine `https://scan.volepapillondamour.fr`, la pousse dans
`vpdacrdev`, puis met à jour `vpd-scan-ca-dev`. Son résumé GitHub fournit l'origine
canonique et le FQDN ACA de secours. L'infrastructure est créée par `Infra - deploy` et
le worker par `Worker - deploy`.

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
