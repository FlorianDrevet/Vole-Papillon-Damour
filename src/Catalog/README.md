# Catalogue public

Le Catalogue est l'application Angular SSR publique de la bourse aux livres,
servie sur `https://livres.volepapillondamour.fr`.

## Consentement et mesure d'audience

Microsoft Clarity et Google Analytics 4 sont chargés dynamiquement uniquement après
un consentement explicite à la mesure d'audience. Le refus reste compatible avec la
consultation anonyme du catalogue. Le lien **Gérer les cookies** du pied de page
permet de modifier le choix ; les détails sont dans
[`/politique-de-cookies`](https://livres.volepapillondamour.fr/politique-de-cookies).

Les identifiants de mesure sont publics, mais ils ne doivent pas être ajoutés comme
secrets :

- variable GitHub Actions `CATALOG_GOOGLE_ANALYTICS_MEASUREMENT_ID` : flux GA4 du
  catalogue (`G-GBHC67EGGF`) ;
- variable GitHub Actions `CLARITY_PROJECT_ID` : projet Clarity du catalogue
  (`yerabb7gnt`).

Le workflow `Catalog - deploy` transmet ces valeurs au Dockerfile, qui les injecte
dans l'environnement de production au moment du build. Le développement local reste
désactivé par défaut.

Ne pas réutiliser `GOOGLE_ANALYTICS_MEASUREMENT_ID`, réservé au Website
(`G-D67DMFCTDG`).

## SEO et Search Console

Les fichiers publics `public/robots.txt` et `public/sitemap.xml` déclarent le domaine
du catalogue. Après un déploiement public, vérifier puis soumettre
`https://livres.volepapillondamour.fr/sitemap.xml` dans la propriété Search Console de
domaine `volepapillondamour.fr` ; aucune propriété supplémentaire n'est nécessaire.

## Mentions à maintenir

Les pages `/mentions-legales`, `/confidentialite`, `/politique-de-cookies` et
`/accessibilite` décrivent le fonctionnement actuel. Toute évolution du compte, des
alertes, des prestataires, des durées de conservation, des transferts ou de l'audit
RGAA doit être répercutée dans ces pages et validée par l'association avant mise en
production.
