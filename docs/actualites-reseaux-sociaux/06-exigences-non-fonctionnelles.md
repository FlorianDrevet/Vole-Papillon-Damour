# 06 — Exigences non fonctionnelles

## Cadence et fraîcheur

| # | Exigence |
|---|---|
| `ENF-ACT-01` | La fonction se réveille **toutes les 30 minutes** (`0 */30 * * * *`). Un brouillon existe donc au plus 30 minutes après la publication sur le réseau, hors validation humaine |
| `ENF-ACT-02` | La cadence est un **paramètre de configuration**, pas une constante compilée. La faire passer à 15 minutes ou à 2 heures ne doit pas demander de livraison |
| `ENF-ACT-03` | Un passage complet, dans le cas nominal (aucune nouvelle publication), tient en **moins de 5 secondes** et consomme deux appels réseau au plus |
| `ENF-ACT-04` | Le Container App du Worker tourne à `minReplicas: 0`. La fonction doit donc supporter un **démarrage à froid** à chaque réveil et n'entretenir aucun état en mémoire entre deux passages |

## Le jeton Meta — le vrai risque d'exploitation

| # | Exigence |
|---|---|
| `ENF-ACT-05` | Le jeton d'accès longue durée expire au bout de **60 jours**. Une alerte est levée **15 jours avant** l'expiration, sur le groupe d'action de supervision existant. Sans cela, l'import s'arrête en silence et personne ne le remarque avant des semaines |
| `ENF-ACT-06` | **Trois passages consécutifs en échec** lèvent une alerte. Un échec isolé n'alerte pas : le réseau a le droit de tousser |
| `ENF-ACT-07` | Une réponse d'authentification refusée (jeton expiré, révoqué, permissions retirées) alerte **immédiatement**, sans attendre `ENF-ACT-06`. Ce n'est pas un incident réseau, c'est une action humaine à mener |

## Quotas et sobriété

| # | Exigence |
|---|---|
| `ENF-ACT-08` | Les quotas de la plateforme Meta se comptent en centaines d'appels par heure. Deux appels toutes les 30 minutes en consomment une fraction négligeable ; aucun mécanisme de limitation propre n'est nécessaire, mais une réponse de type « quota dépassé » doit être **journalisée comme telle** et non confondue avec une panne |
| `ENF-ACT-09` | Le plafond de `RG-ACT-17` (5 actualités par passage) borne aussi bien les appels au modèle que le téléversement d'images. C'est le garde-fou de coût du dispositif |

## Données personnelles et droit à l'image

| # | Exigence |
|---|---|
| `ENF-ACT-10` | L'import ne crée **aucune collecte nouvelle** : il recopie un contenu que l'association a elle-même publié. Il change en revanche le **support** — un réseau social fermé aux non-inscrits devient un site public indexable — et cela seul justifie la validation humaine de `RG-ACT-04` |
| `ENF-ACT-11` | L'administrateur qui valide un brouillon est **le point de contrôle** du droit à l'image, en particulier pour les photos où des enfants sont reconnaissables. L'écran de relecture doit montrer **toutes** les images en taille suffisante pour que ce contrôle soit réel, pas théorique |
| `ENF-ACT-12` | Le modèle de langage ne reçoit **que le texte**, jamais les images (`05`, section 6) |
| `ENF-ACT-13` | Les journaux ne contiennent **ni la légende, ni le titre généré, ni les URL d'images**. Ils portent l'identifiant externe, le compte, l'issue et la durée. Un journal n'est pas un endroit où reconstituer du contenu personnel |
| `ENF-ACT-14` | La suppression d'une actualité depuis le BackOffice supprime **aussi** les images recopiées dans le stockage. Sinon la suppression est un affichage, pas un effacement |

## Conditions d'utilisation des plateformes

| # | Exigence |
|---|---|
| `ENF-ACT-15` | L'accès aux contenus passe **exclusivement** par les API officielles de Meta, avec des permissions accordées en revue d'application. Aucun scraping HTML, aucune « API non officielle », aucun service tiers qui en fait usage (`02`, section 5) |
| `ENF-ACT-16` | La version de la Graph API utilisée est **explicite** dans la configuration. Meta déprécie chaque version en environ deux ans : la montée de version doit être un geste conscient, pas une panne découverte un lundi matin |
| `ENF-ACT-17` | L'application Meta appartient à l'association, pas à une personne physique (`Q-ACT-04`) |

## Robustesse

| # | Exigence |
|---|---|
| `ENF-ACT-18` | Une panne de l'import, quelle qu'en soit la cause, **laisse le site intact**. Le site continue de servir ses actualités publiées, et la saisie manuelle reste disponible en permanence |
| `ENF-ACT-19` | L'import est **idempotent** : le rejouer sur les mêmes publications ne crée pas de doublon (`RG-ACT-02`). C'est ce qui autorise à retenter sans réfléchir |
| `ENF-ACT-20` | Deux passages ne peuvent pas se chevaucher. Le déclencheur minuteur des Functions le garantit par instance ; avec `maxReplicas: 1` sur le Worker, la garantie est suffisante |
| `ENF-ACT-21` | Toute écriture partielle est impossible : une actualité est créée avec **toutes** ses images ou pas du tout (`RG-ACT-19`) |

## Observabilité

| # | Exigence |
|---|---|
| `ENF-ACT-22` | Chaque passage journalise, au niveau `Information` : le nombre de publications examinées, importées, ignorées comme déjà connues, échouées, et le nombre de titres de repli. Même forme que les fonctions `Enrich` et `Sweep` déjà en place |
| `ENF-ACT-23` | Les deux causes de « pas d'actualité » — définitive (`RG-ACT-12`) et temporaire (`RG-ACT-19`) — sont distinguables dans les journaux. Sans cela l'exploitation est aveugle |
| `ENF-ACT-24` | Le nombre de brouillons en attente est visible dans le BackOffice (`03`, section 3). C'est la seule mesure qui compte au quotidien |

## Accessibilité et rendu

| # | Exigence |
|---|---|
| `ENF-ACT-25` | Aucune régression d'accessibilité : les images importées suivent le traitement existant des images d'actualité. Le texte alternatif **n'est pas généré par IA** en v1 (`Q-ACT-06`) |
| `ENF-ACT-26` | Le texte importé s'affiche en respectant ses paragraphes (`DT-ACT-07`). Sans ce correctif, toute légende multi-paragraphes devient un bloc illisible |
| `ENF-ACT-27` | Le poids des images recopiées est celui servi par Meta, sans retraitement en v1. Si les temps de chargement se dégradent, la compression sera un sujet à part entière, pas un ajout discret à ce projet |
