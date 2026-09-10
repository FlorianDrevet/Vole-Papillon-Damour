# 03 — Parcours : du post au site

## 1. Le trajet complet

```
La présidente publie sur Facebook
        │  (diffusion croisée cochée)
        ▼
Le post apparaît sur instagram.com/vole_papillon_damour
        │
        │  ≤ 30 min
        ▼
[Minuteur]  La fonction d'import se réveille dans le Worker
        │
        ├─ 1. Liste les médias récents du compte
        ├─ 2. Écarte ceux déjà importés (RG-ACT-02) et ceux antérieurs
        │     à la date plancher (RG-ACT-03)
        ├─ 3. Pour chaque nouveau média :
        │       ├─ nettoie la légende            → Article   (RG-ACT-07)
        │       ├─ télécharge et recopie les images dans
        │       │  le conteneur actuality-images (RG-ACT-10)
        │       ├─ demande un titre au modèle    → Titre     (05)
        │       └─ crée l'actualité à l'état Brouillon       (RG-ACT-04)
        ▼
[BackOffice]  L'administrateur voit « 1 brouillon à relire »
        │
        ├─ relit le titre, le texte, les images
        ├─ corrige ce qu'il faut
        │
        ├─ Publier  ──────────────────────────► en ligne sur le site
        └─ Écarter  ──────────────────────────► brouillon supprimé,
                                                jamais réimporté (RG-ACT-02)
```

Le site public ne connaît que la dernière étape : il sert des actualités publiées, sans
savoir d'où elles viennent.

## 2. Le passage de la fonction, en détail

Un passage est **atomique par publication** : si le traitement d'un média échoue, les
autres médias du même passage sont quand même créés. Un média en échec n'est pas marqué
comme importé, il sera donc retenté au passage suivant.

| Étape | Comportement en cas d'échec |
|---|---|
| Appel à l'API Meta | Le passage s'arrête, sans effet de bord. Retenté dans 30 min. Trois échecs consécutifs déclenchent une alerte (`ENF-ACT-06`) |
| Jeton expiré ou révoqué | Idem, avec une alerte distincte et immédiate — c'est une panne humaine, pas un incident réseau (`ENF-ACT-05`) |
| Téléchargement d'une image | Le média entier est abandonné pour ce passage. Une actualité amputée d'une photo serait pire qu'une actualité en retard |
| Génération du titre | L'actualité **est créée quand même**, avec le titre de repli et le marqueur « titre à revoir » (`RG-ACT-20`) |
| Écriture en base | Le média n'est pas marqué importé ; retenté au passage suivant. L'unicité `(Source, ExternalId)` protège des doublons en cas de retentative partielle (`RG-ACT-02`) |

## 3. L'écran de relecture (BackOffice)

Le BackOffice a déjà une gestion des actualités (`feature/actualities`, groupées par
mois). L'import s'y insère sans écran nouveau, avec quatre ajouts :

1. **Un bandeau de tête** : « *n* brouillon(s) en attente de relecture », qui filtre la
   liste. Absent quand il n'y en a aucun.
2. **Une pastille d'état** sur chaque carte : `Brouillon` / `Publiée`, et pour un
   brouillon importé, la mention de sa provenance avec un lien vers la publication
   d'origine.
3. **Un marqueur « titre à revoir »** quand le titre est de repli ou généré, retiré dès
   que l'administrateur enregistre. Le titre généré n'est pas signalé au visiteur : ce
   n'est pas une information qui le concerne.
4. **Le formulaire d'édition existant, inchangé**, prérempli. Deux boutons distincts :
   *Enregistrer le brouillon* et *Publier*.

**Rien n'est retiré.** La création manuelle d'une actualité reste exactement ce qu'elle
est aujourd'hui — c'est le filet de sécurité du jour où Meta coupe l'accès. Une actualité
créée à la main naît directement `Publiée`, comme aujourd'hui.

## 4. Ce que voit le visiteur

| Élément | Aujourd'hui | Après |
|---|---|---|
| Liste des actualités | Toutes les actualités | Uniquement les actualités **publiées** (`RG-ACT-05`) |
| Les 3 dernières en page d'accueil | Les 3 plus récentes par date | Les 3 plus récentes **publiées** |
| Fiche d'une actualité | Titre, date, image, texte, galerie, liens réseaux | Identique. Le texte respecte enfin les paragraphes (`DT-ACT-07`) |
| Origine de l'actualité | — | **Non affichée.** Le lien « Réagissez à cette actualité » vers Instagram existe déjà et suffit |

⚠️ **Point de vigilance sur le rendu du texte.** Aujourd'hui l'article est rendu dans un
unique `<p>{{ article.article }}</p>` : les retours à la ligne sont avalés par HTML. Une
légende Instagram, elle, est presque toujours en plusieurs paragraphes. Sans le
correctif `DT-ACT-07`, toute actualité importée s'afficherait en un seul bloc compact.
C'est une correction d'une ligne, mais elle est **obligatoire** et fait partie du palier
`L1`.

## 5. Le cas de la première mise en service

Au tout premier passage, le compte Instagram contient tout l'historique de
l'association. Sans garde-fou, la fonction créerait des dizaines de brouillons d'un coup
et consommerait autant d'appels au modèle.

Deux protections, cumulées :

- **Une date plancher** (`RG-ACT-03`) : rien d'antérieur n'est jamais regardé. Elle est
  fixée au jour de la mise en service.
- **Un plafond par passage** (`RG-ACT-17`) : au plus 5 actualités créées par réveil. Le
  reste attend le passage suivant. Protège aussi la reprise après une panne longue.

La reprise de l'historique ancien, si elle est souhaitée un jour, sera un geste manuel
et explicite — pas un effet de bord de la mise en service.
