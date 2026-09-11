# 04 — Règles métier

Règles numérotées, vérifiables, référencées depuis les autres documents. Chacune doit
pouvoir se traduire en test.

## Source et déclenchement

| # | Règle |
|---|---|
| `RG-ACT-01` | La **seule** source lue en v1 est le compte Instagram professionnel de l'association. Le profil Facebook personnel n'est jamais interrogé (`02`, section 2) |
| `RG-ACT-02` | Une publication source n'est importée **qu'une fois**. Le couple (source, identifiant externe) est unique en base. Un brouillon supprimé n'est pas réimporté : la trace d'import survit à la suppression de l'actualité |
| `RG-ACT-03` | Aucune publication antérieure à la **date plancher** configurée n'est importée, quelle que soit la raison du passage |
| `RG-ACT-17` | Au plus **5 actualités** sont créées par passage. Le surplus attend le passage suivant. Le plafond est configurable |

## État et visibilité

| # | Règle |
|---|---|
| `RG-ACT-04` | Une actualité importée naît à l'état **`Brouillon`**. Elle ne devient `Publiée` que par une action explicite d'un administrateur |
| `RG-ACT-05` | Les lectures publiques (`/actuality/all`, `/actuality/latest`, `/actuality/{id}`) ne renvoient que les actualités **`Publiée`**. Une actualité en brouillon demandée par son identifiant répond « non trouvée » — pas « interdite », qui révélerait son existence |
| `RG-ACT-06` | Une actualité créée à la main dans le BackOffice naît **`Publiée`**. Le comportement d'aujourd'hui ne change pas |
| `RG-ACT-16` | Un brouillon non traité depuis **30 jours** est signalé dans le BackOffice. Il n'est **jamais** supprimé automatiquement |

## Contenu

| # | Règle |
|---|---|
| `RG-ACT-07` | La légende de la publication devient l'article, après nettoyage : suppression du bloc de hashtags terminal, suppression des hashtags isolés en fin de ligne, réduction des lignes vides consécutives à une seule, suppression des espaces de fin de ligne. Les retours à la ligne restants, les emoji et les mentions `@` sont **conservés** |
| `RG-ACT-08` | Aucun texte n'est inventé ni reformulé. Si la légende est vide après nettoyage, l'article est vide et l'actualité reste en brouillon, à écrire à la main |
| `RG-ACT-13` | La date de l'actualité est l'horodatage de la publication source. Stockée en UTC, affichée en `Europe/Paris`, conformément au traitement du temps déjà en vigueur dans le projet |
| `RG-ACT-14` | Le lien Instagram de l'actualité est le lien permanent de la publication source. Le lien Facebook **n'est pas renseigné** en v1 : rien dans les données Instagram ne permet de retrouver le post Facebook correspondant |

## Images

| # | Règle |
|---|---|
| `RG-ACT-09` | Le premier média de la publication devient l'**image principale**. Les suivants, dans l'ordre du carrousel, alimentent la galerie |
| `RG-ACT-10` | **Toute image est téléchargée et recopiée** dans le conteneur `actuality-images`. Aucune URL de CDN Meta n'est jamais stockée en base : ces URL sont signées et expirent |
| `RG-ACT-11` | Pour un média vidéo, la **vignette** sert d'image. La vidéo n'est pas rapatriée en v1 ; le lien vers la publication d'origine tient lieu d'accès |
| `RG-ACT-12` | Une publication sans média exploitable **ne produit pas d'actualité**. Un échec explicite est journalisé. Motif : l'image principale est obligatoire dans le modèle de données |
| `RG-ACT-18` | Au plus **10 images** par actualité — le maximum d'un carrousel Instagram. Au-delà, les images excédentaires sont ignorées et l'écart est journalisé |
| `RG-ACT-19` | Une image dont le téléchargement échoue fait échouer l'import de **toute** la publication, qui sera retentée. Une actualité amputée d'une photo est pire qu'une actualité en retard |

## Titre

| # | Règle |
|---|---|
| `RG-ACT-15` | Le titre est **proposé** par un modèle de langage à partir du seul texte de la publication. Il est toujours modifiable et n'est jamais publié sans relecture, puisque l'actualité naît en brouillon |
| `RG-ACT-20` | Si la génération échoue, est vide, ou viole une contrainte de `05`, le **titre de repli** est utilisé : « Actualité du *j mois aaaa* ». L'actualité est créée quand même et marquée « titre à revoir » |
| `RG-ACT-21` | Le marqueur « titre à revoir » disparaît dès le premier enregistrement par un administrateur, qu'il ait modifié le titre ou non |

## Vie après l'import

| # | Règle |
|---|---|
| `RG-ACT-22` | La modification ou la suppression de la publication sur le réseau **est sans effet** sur l'actualité déjà importée. Les deux vivent séparément |
| `RG-ACT-23` | Une actualité importée puis publiée se modifie et se supprime exactement comme une actualité saisie à la main. L'origine ne confère aucun statut particulier |

## Ce que les règles impliquent, et qu'il faut avoir en tête

- `RG-ACT-05` change le comportement d'endpoints **existants et déjà consommés par le
  site**. C'est le seul point du projet qui touche à du code en production sur un chemin
  public : il mérite un test de non-régression explicite.
- `RG-ACT-02` impose que la trace d'import survive à la suppression de l'actualité.
  Conserver l'identifiant externe dans la ligne supprimée n'est donc pas possible si la
  suppression est physique — voir `technique/` pour le traitement retenu.
- `RG-ACT-12` et `RG-ACT-19` produisent tous deux « pas d'actualité », mais pour des
  raisons opposées : l'une est définitive, l'autre est temporaire et sera retentée. Les
  journaux doivent les distinguer, sinon l'exploitation est aveugle.
