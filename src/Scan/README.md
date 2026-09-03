# Sonde de scan

La sonde `S0-2` est une application Angular en consultation seule. Elle accepte un
ISBN saisi au clavier, envoyé par une scanette USB, ou lu par la caméra du navigateur,
puis interroge `GET /books/{isbn13}/metadata` sur l'API locale. Elle n'enregistre ni
session, ni file, ni donnée en base.

## Lancer la sonde

Depuis `src/Scan` :

```bash
npm ci
npm start
```

Le lancement de l'AppHost est recommandé pour démarrer l'API et la sonde ensemble :

```bash
dotnet run --project ../Backend/Vole_Papillon_Damour.AppHost
```

Pour une campagne sur téléphone, ouvrir `http://<IP-DU-PORTABLE>:4202`. En mode
développement, l'URL de l'API est construite avec le même nom d'hôte et le port `5257`;
le portable et le téléphone doivent donc être sur le même réseau. L'accès caméra exige
un contexte sécurisé (`localhost` ou HTTPS) selon les règles du navigateur ; si le
navigateur du téléphone le refuse sur HTTP LAN, utiliser la saisie clavier ou configurer
un tunnel/HTTPS local.

## Vérifications

```bash
npm test -- --watch=false --browsers=ChromeHeadless
npm run build
```
