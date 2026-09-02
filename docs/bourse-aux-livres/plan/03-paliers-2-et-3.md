# Lot 3 — Paliers 2 et 3, vitrine puis alertes

**Ce fichier est volontairement grossier.** Il donne les jalons, les points durs connus et
les décisions restant à prendre — pas des étapes numérotées.

**Pourquoi.** Le principe directeur n°4 de l'architecture — « ce qui n'est pas mesuré n'est
pas décidé » — vaut aussi pour le plan. Détailler ces deux paliers aujourd'hui, c'est
écrire des étapes contre des hypothèses que `QT-01`, `QT-03` et la bourse d'épreuve du
palier 1 vont confirmer ou casser. On les détaille quand le palier 1 est validé.

Ce qui suit sert à **ne pas oublier ce qui est déjà su**, et à repérer ce qui a un délai.

---

## Palier 2 — La vitrine

**Dépend de** : palier 1 validé. Publier un catalogue sur un stock non fiable serait
contre-productif.

### Ce qui est déjà décidé

| Sujet | Décision |
|---|---|
| Application | Angular avec SSR, distincte du `Website` et du `BackOffice` (`01` §6 fonctionnel) |
| Adresse | `livres.volepapillondamour.fr`, certificat managé gratuit (`DT-13`) |
| URL d'une fiche | `/livres/{slug-titre-auteur}-{isbn13}` |
| Page d'œuvre | `/oeuvre/{workId}`, canonique (`DT-13`) |
| Recherche | Plein texte SQL Server d'abord, Azure AI Search différé (`DT-07`) |
| Deux périmètres | Catalogue et référentiel externe, **jamais mélangés** (`RG-47`) |
| Mesure d'audience | **Aucun traceur.** Ni GA4, ni équivalent — `ENF-14`, et voir ci-dessous |

### Les points durs

**Le référencement ne s'arrête pas au SSR** (`05` §1, `revue.md` `R-09`). Sitemap dynamique
découpé pour quinze mille fiches — le sitemap actuel du `Website` est un fichier statique
—, canoniques entre éditions, `robots.txt` propre à l'application, données structurées
`schema.org/Book`.

**La décision qui reste à prendre** : que faire des fiches épuisées. `RG-26` les maintient
au catalogue, et c'est non négociable — c'est le cas d'usage central des alertes. Mais cela
produit des milliers de pages à contenu très mince, le profil exact que les moteurs
déclassent, et qui peut entraîner le reste du domaine avec lui. Canonisation vers l'œuvre,
ou `noindex` sous un seuil de contenu. À trancher **avant la première indexation**.

**GA4 est un piège de copier-coller.** Le `Website` existant l'embarque, injecté au build
par `website-deploy.yml`. Le réflexe en créant une troisième application Angular sera de
reprendre la configuration — et de mettre l'association en défaut sur sa propre exigence
(`revue.md` `R-17`). La règle de `11` §7 s'applique : télémétrie sur la zone
d'administration, **jamais** sur les pages publiques.

**L'écran de désengorgement** (`05` §5) porte la requête la plus lourde du système. Elle
n'est consultée que quelques fois par mois, donc aucune optimisation prématurée — mais un
index adapté dès l'écriture.

### Le test manuel qui compte

Publier une poignée de fiches, puis **demander l'indexation dans la Search Console** et
vérifier ce qui est réellement indexé, comment le titre et la description apparaissent, et
que les canoniques sont respectées. Le faire sur quelques fiches avant d'en publier quinze
mille : une erreur de canonique découverte après coup se paie en mois.

---

## Palier 3 — Les alertes

**Dépend de** : palier 2 en production et alimenté.

Le fournisseur d'identité et la messagerie sont en place **depuis le lot 0**. Ce palier
ouvre l'inscription en libre-service, il ne la construit pas — et le domaine d'envoi aura
chauffé pendant des mois.

### Ce qui est déjà décidé

| Sujet | Décision |
|---|---|
| Envoi | Azure Communication Services, sous-domaine `mail.` (`DT-12`) |
| Regroupement | Un e-mail par membre et par session (`RG-29`) |
| Délai | Mise en file à la clôture, envoi 2 h plus tard (`RG-44`), paramétrable |
| Anti-répétition | `UserAlertHistory`, vérifiée **deux fois** — indicative à la clôture, faisant foi à l'envoi (`02` §2) |
| Personnes | Une seule table, `Watchlist` pour la facette membre (`DT-14`) |
| Rebonds | Rapports ACS via Event Grid (`07` §7) |

### Les points durs

**`R-06` reste ouvert et doit être fermé ici** : la suppression du compte **dans le
locataire**. `ENF-12` promet un effacement effectif ; effacer nos données en laissant
l'identité vivante n'est pas une suppression. Cela suppose un appel Microsoft Graph
applicatif, donc un enregistrement d'application, un secret, et une exposition à
l'authentification M2M que `QT-04` déclarait nulle. À instruire avant d'ouvrir
l'inscription, pas après avoir des comptes à supprimer.

**Le repli de `RG-46`.** Si `QT-01` a montré une couverture insuffisante en `WorkId`, le
rapprochement par titre + auteur normalisés devient obligatoire. Il produit des faux
positifs sur les séries, les homonymes et les adaptations — retenu quand même, parce qu'un
membre prévenu à tort coûte moins cher qu'un membre jamais prévenu.

**L'ajout à la liste de recherche est l'écran le plus subtil du site** (`05` §4). Deux
erreurs à ne pas commettre : proposer l'édition avant l'œuvre — dans une bourse à 1–2 €,
la quasi-totalité des gens cherchent un texte, pas un tirage —, et masquer ensuite la
portée choisie.

**`QT-07` doit avoir été vérifiée au lot 0.** Si aucune configuration ne donne la connexion
seule, ouvrir l'inscription sur le catalogue ouvre aussi la création de comptes ailleurs.

### Les tests manuels qui comptent

- **Un cycle complet, de bout en bout** : s'inscrire, ajouter une œuvre à sa liste, faire
  scanner une édition de cette œuvre par un bénévole, clôturer la session, et **recevoir
  l'e-mail deux heures plus tard** — en boîte de réception, pas en indésirables.
- **La fenêtre de rattrapage** : refaire la même chose, puis corriger la session dans le
  délai, et vérifier qu'aucun e-mail ne part.
- **La suppression de compte**, et vérifier des deux côtés : plus rien chez nous, plus rien
  dans le locataire.
- **Le rebond** : envoyer vers une adresse inexistante, et vérifier que `BounceCount`
  s'incrémente et que la suspension se déclenche au seuil.

---

## Ce qui reste ouvert après le palier 3

Deux manques relevés en revue n'appartiennent à aucun palier et devront trouver une place :

**Le repli d'exploitation de `ENF-21`** — « une indisponibilité ne doit jamais empêcher de
vendre », présenté comme le critère qui prime sur tout le reste. Aucune procédure n'est
écrite : que fait le caissier si son appareil tombe en panne un jour de bourse ? Feuille de
papier, puis quelle saisie, par quel écran ? À traiter au plus tard avant la première
bourse du palier 1.

**La stratégie de test des fronts.** `03` §6 traite bien le backend. Rien sur le mode hors
ligne — le chemin le plus critique et le plus difficile à éprouver à la main —, ni sur la
survie de la file de sortie, ni sur un jeu de données de démonstration permettant de
rejouer une session.
