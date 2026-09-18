# Maquettes — module Livres rares

Les maquettes vivent dans le projet Claude Design « Catalogue Livres »
(`e18d5045-653e-4cc5-b2c0-f29ab5ba1bd2`), qui n'est accessible qu'avec le compte
claude.ai de l'association. Ce dossier existe pour que **n'importe quel outil** — un
agent qui n'a pas ce compte, une relecture dans une PR, une consultation dans deux ans —
puisse s'y référer sans dépendre de cet accès.

## Ce qui est ici

| Fichier | Contenu |
|---|---|
| `ScanLivresRares.dc.html` | S1 · Scanette — accueil avec la tuile, liste des fiches, fourche d'ajout |
| `ScanSaisieRare.dc.html` | S2 · Scanette — candidat BnF, puis les trois temps du formulaire |
| `CaisseAjoutRare.dc.html` | S3 · Caisse — bouton, recherche par titre, clôture de session |
| `canvas.json` | La disposition des trois pages du canvas et les notes qui les accompagnent |

Ce sont des fichiers HTML autonomes, à styles inline : ils se lisent aussi bien comme
source que comme page. Ils s'ouvrent dans un navigateur, avec deux réserves — la ligne
`<script src="./support.js">` et les balises `<dc-import>` ne résolvent rien hors du
canvas, et les polices viennent de Google Fonts.

## Ce qui n'est pas ici, et pourquoi

Les cinq artboards antérieurs — `LivresRares`, `FicheRare`, `LivresRaresMobile`,
`AdminFicheRare`, `CaisseRare` — n'ont pas été recopiés. Les recopier à la main aurait
introduit un risque de dérive silencieuse sur un fichier censé faire référence.

Deux façons fiables de les récupérer :

1. **Export PDF depuis le canvas** (un clic, sans perte, toutes les pages d'un coup) et
   déposer le fichier ici sous `maquettes-canvas.pdf`. C'est la voie recommandée : un
   PDF se lit par un humain comme par un agent.
2. **Depuis une session Claude Code** avec l'outil `DesignSync` et le compte de
   l'association : `get_file` sur le chemin voulu.

## L'essentiel n'est pas dans les maquettes

Les valeurs qui comptent — couleurs, tailles, espacements, libellés français exacts —
ont été relevées sur ces artboards et **écrites dans le plan** :

- [`05-catalogue-public.md`](../05-catalogue-public.md) pour la liste et la fiche
  publiques ;
- [`06-portail-administration.md`](../06-portail-administration.md) pour l'onglet
  d'administration et le formulaire ;
- [`07-scanette.md`](../07-scanette.md) pour la gestion et la caisse.

Une session qui n'a accès qu'au dépôt a donc de quoi implémenter fidèlement. Les
maquettes servent à vérifier une intention et une composition, pas à retrouver un
nombre.

## Un avertissement à ne pas manquer

L'artboard `CaisseRare` (A12) montre un panier et un total « 63,00 € encaissés ».
**Cette partie est caduque** : l'association a tranché que l'application ne compte aucun
argent. Voir [`01 §2 D4`](../01-decisions-et-portee.md). Le reste de A12 — le bandeau
violet, le prix ferme en grand, la recherche par titre — reste la référence.
