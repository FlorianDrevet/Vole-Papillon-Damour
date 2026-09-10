# 01 — Vision et périmètre

## 1. Problème

Le site publie des actualités. Elles sont saisies une par une dans le BackOffice :
titre, article, image principale, images secondaires, date, liens Facebook et Instagram
(`POST /actuality`, réservé aux administrateurs).

La même information existe déjà ailleurs, écrite avant. La présidente publie sur
Facebook depuis son profil `melvin.drevet.1`, et coche la diffusion croisée vers
Instagram, ce qui fait apparaître le même contenu sur `vole_papillon_damour`. La saisie
dans le BackOffice est donc une **recopie**, faite plus tard, par quelqu'un d'autre,
avec les photos à re-télécharger et à re-téléverser une à une.

Trois conséquences observables :

1. Le site est **en retard** sur les réseaux, parfois de plusieurs semaines.
2. Certaines publications ne sont **jamais** reprises : la recopie ne se fait pas.
3. Le site est le seul canal que l'association possède réellement — les réseaux ne lui
   appartiennent pas — et c'est celui qui est le moins alimenté.

## 2. Objectifs

| # | Objectif | Comment on saura que c'est atteint |
|---|---|---|
| O1 | Supprimer la double saisie | Une actualité issue des réseaux est publiable sans qu'aucun texte ni aucune image n'ait été ressaisi |
| O2 | Réduire le retard du site sur les réseaux | Le brouillon existe moins de 30 minutes après la publication sur le réseau (`ENF-ACT-01`) |
| O3 | Ne plus perdre de publications | Toute publication de la source depuis la date plancher a soit une actualité, soit une trace d'échec explicite (`RG-ACT-12`) |
| O4 | Ne rien perdre du contrôle éditorial | Aucune actualité importée n'est visible publiquement sans validation humaine (`RG-ACT-04`) |
| O5 | Donner un titre au site là où le réseau n'en a pas | Le titre proposé est retenu tel quel dans la majorité des cas ; il est toujours modifiable (`05`) |

**Non-objectif assumé :** faire du site un miroir des réseaux. Le site n'a pas vocation
à tout reprendre, et la validation humaine est le filtre.

## 3. Acteurs

| Acteur | Rôle | Contexte d'usage |
|---|---|---|
| **Présidente** | Publie sur les réseaux, comme aujourd'hui | Depuis son téléphone, hors de tout outil de l'association. **Son geste ne change pas** |
| **Administrateur du site** | Relit, corrige, publie ou écarte le brouillon | Sur ordinateur, dans le BackOffice, quelques minutes par semaine |
| **Visiteur du site** | Lit les actualités | Ne voit jamais un brouillon, ne sait pas d'où vient l'actualité |
| **La fonction d'import** | Va chercher les publications, prépare les brouillons | Automatique, toutes les 30 minutes, sans supervision |

## 4. Ce qui est dans le périmètre

- Lecture périodique des publications du compte Instagram professionnel de
  l'association (`02` explique pourquoi Instagram et pas Facebook).
- Récupération du texte de la publication, des images du carrousel et de la date.
- **Recopie des images dans le stockage de l'association**, jamais un lien vers le CDN
  de Meta (`RG-ACT-10`).
- Génération d'un **titre proposé** par un modèle Azure AI Foundry (`05`).
- Création d'une actualité à l'état **brouillon**, invisible du site public.
- Écran de relecture et de publication dans le BackOffice.
- Lien retour vers la publication d'origine, affiché sur la fiche de l'actualité (le
  bloc « Réagissez à cette actualité » existe déjà).
- Alerte avant expiration de l'autorisation d'accès Meta (`ENF-ACT-05`).

## 5. Ce qui est hors périmètre

| Hors périmètre | Pourquoi |
|---|---|
| Lire le **profil Facebook personnel** de la présidente | Meta ne l'autorise pas pour cet usage (`02`, section 2) |
| Publier **depuis** le site vers les réseaux | Sens inverse, autre projet, autres autorisations |
| Reprendre les **stories** | Éphémères par nature, sans intérêt pour un site permanent |
| Rapatrier les **vidéos** | Le modèle `Actuality` n'a pas de champ vidéo. La vignette suffit en v1 (`RG-ACT-11`) |
| Suivre les **modifications** d'un post déjà importé | Le post source et l'actualité vivent leur vie séparément (`RG-ACT-22`) |
| Importer les **commentaires** et les réactions | Le lien vers la publication d'origine y renvoie déjà |
| Générer le **texte alternatif** des images par IA | Risque d'invention sur des photos de personnes (`Q-ACT-06`) |
| Un **déclencheur événementiel** | Techniquement impossible sur Instagram (`02`, section 3). Reporté avec la page Facebook (`Q-ACT-03`) |

## 6. Paliers de livraison

L'ordre est un **ordre de construction**, pas un calendrier. Chaque palier est
livrable seul et laisse le site en état de marche.

| Palier | Contenu | Ce qu'on peut faire à la fin |
|---|---|---|
| **L1 — Le socle éditorial** | Statut `Brouillon`/`Publiée` sur `Actuality`, filtrage des lectures publiques, écran de relecture BackOffice, rendu des retours à la ligne sur le site (`DT-ACT-07`) | Créer un brouillon à la main et le publier. **Aucune dépendance à Meta** : ce palier se livre même si `Q-ACT-01` n'a pas de réponse |
| **L2 — L'import** | Client Instagram, fonction minuteur dans le Worker existant, recopie des images, idempotence | Les publications arrivent en brouillon avec un titre de repli |
| **L3 — Le titre** | Modèle Azure AI Foundry, garde-fous, repli | Le titre proposé est exploitable tel quel |
| **L4 — L'exploitation** | Alerte d'expiration du jeton, alerte d'échec répété, garde-fou de volume, brouillons dormants | On peut ne plus y penser |
| **L5 — La page Facebook** *(v2, conditionné à `Q-ACT-03`)* | Page de l'association, abonnement au webhook `feed`, fonction HTTP | L'actualité arrive **au moment** de la publication, et l'association ne dépend plus d'un profil personnel |

## 7. Ce qu'on ne veut surtout pas

- **Une publication automatique sans relecture.** Une erreur sur un réseau se corrige
  en trente secondes ; une erreur sur un site indexé par Google vit plus longtemps.
- **Un scraping HTML d'Instagram ou de Facebook.** Interdit par les conditions
  d'utilisation de Meta, cassé à chaque changement de page, et impossible à défendre
  auprès de l'association. Si l'API officielle ne permet pas quelque chose, on ne le
  fait pas.
- **Une dépendance silencieuse.** Le jour où Meta coupe l'accès, il faut que ça se voie
  dans une alerte, pas dans un site qui cesse doucement d'avoir des actualités.
