# Lot 3 — Paliers 2 et 3, vitrine puis alertes

**Ce fichier est volontairement grossier.** Il donne les jalons, les points durs connus et
les décisions restant à prendre — pas des étapes numérotées.

**Pourquoi.** Le principe directeur n°4 de l'architecture — « ce qui n'est pas mesuré n'est
pas décidé » — vaut aussi pour le plan. Détailler ces deux paliers aujourd'hui, c'est
écrire des étapes contre des hypothèses que `QT-01`, `QT-03` et la répétition générale du
palier 1 vont confirmer ou casser. On les détaille quand le palier 1 tient.

Ce qui suit sert à **ne pas oublier ce qui est déjà su**, et à repérer ce qui a un délai.

## État d'exécution — 2026-09-05

Le palier 2 est livré sur `main` (`3a6e887`) et en production DEV : catalogue SSR, domaines publics,
recherche, pages d'œuvre, sitemap, robots et absence de traceurs. Le palier 3 a été engagé
au-delà de la présente esquisse : connexion Entra du catalogue, watchlists édition/œuvre,
suppression de compte, file d'alertes, rebonds et lecture d'administration du stock mort sont
implémentés et testés. La PR #61 ajoute la séparation provider-neutral du lookup de suppression
et le nettoyage transactionnel des projections strictement membre avant suppression ou
anonymisation, sans effacer le ledger historique requis. Le runtime API/Worker est déployé
avec le tag `dcc0c23` par `Books runtime - deploy` `33929828651`. La PR #63 a corrigé la
meta robots statique des routes privées et le recalcul de la directive sur navigation ; le
catalogue a été redéployé avec `vpd-catalog:3a6e887` par `Catalog - deploy` `33932087193`,
avec smoke public/privé cohérent. La messagerie ACS reste
volontairement inactive tant que le domaine d'envoi n'est pas vérifié ; le cycle d'alerte
complet, les heartbeats et les validations manuelles restent les prochaines preuves à produire.

---

## Palier 2 — La vitrine

**Dépend de** : le palier 1 tient. Publier un catalogue sur un stock non fiable serait
contre-productif.

### Ce qui est déjà décidé

| Sujet | Décision |
|---|---|
| Application | Angular avec SSR, distincte du `Website` et du `BackOffice` (`F-01` §6) |
| Adresse | `livres.volepapillondamour.fr`, certificat managé gratuit (`DT-13`) |
| URL d'une fiche | `/livres/{slug-titre-auteur}-{isbn13}` |
| Page d'œuvre | `/oeuvre/{workId}`, canonique (`DT-13`) |
| Recherche | Plein texte SQL Server d'abord, Azure AI Search différé (`DT-07`) |
| Deux périmètres | Catalogue et référentiel externe, **jamais mélangés** (`RG-47`) |
| Mesure d'audience | **Aucun traceur.** Ni GA4, ni équivalent — `ENF-14`, et voir ci-dessous |

### Ce qui a un délai, et se lance tôt dans le palier

**Le DNS du catalogue se pose ici, pas au lot 0.** Une version antérieure du plan le mettait
dans le préalable « même si l'application n'existe pas encore ». Ce n'est pas tenable : le
`CNAME` de `livres` pointe vers le FQDN de la Container App du catalogue, et la
vérification `asuid` comme l'émission du certificat managé se font à la liaison du domaine.
L'ordre réel est donc : créer la Container App, puis poser `CNAME` + `TXT asuid`, puis lier
le domaine, puis attendre le certificat. À faire **dès que l'application existe**, même
vide, pour que la propagation et le certificat ne soient pas sur le chemin critique de la
mise en ligne. La propriété Search Console, elle, est posée depuis `L0-8`.

Cette séquence a été exécutée pour `livres.volepapillondamour.fr` : le CNAME OVH, le TXT
`asuid`, la liaison ACA et le certificat managé SNI sont vérifiés ; elle ne constitue donc
plus un blocage du palier 2.

**Les genres viennent des sources, et rien n'indique où est le livre.** `Q-07` est
tranchée : le filtre par genre s'appuie sur ce que renvoient les sources bibliographiques,
normalisé, et sur rien d'autre. Pas de nomenclature maison calquée sur les rayons, donc pas
de correspondance à tenir à la main — et **aucune indication d'emplacement nulle part** :
ni sur la fiche, ni dans les résultats, ni dans une alerte. Le système suit des ISBN et des
quantités, pas des exemplaires : il ne sait pas où est un livre, et l'annoncer serait
promettre une précision qu'il n'a pas.

*Conséquence à assumer en écrivant l'écran* : les genres des sources sont lacunaires. Le
filtre est un confort, pas un classement — un livre sans genre doit rester parfaitement
trouvable par la recherche, qui est le chemin principal. Ne pas construire une navigation
qui suppose que chaque fiche a un genre.

**Les mentions légales et la politique de confidentialité** (`F-04` §2, `ENF-10`,
`ENF-11`) sont des pages à écrire, pas du code, et elles conditionnent la mise en ligne :
le site collecte des adresses e-mail dès le palier 3 et le catalogue est public dès
celui-ci. À rédiger pendant le palier 2, pas la veille de l'ouverture. `ENF-14` — aucun
traceur — a au moins l'avantage de rendre le bandeau de cookies inutile.

### Les points durs

**Le référencement ne s'arrête pas au SSR** (`T-05` §1, `revue.md` `R-09`). Sitemap
dynamique découpé pour quinze mille fiches — le sitemap actuel du `Website` est un fichier
statique —, canoniques entre éditions, `robots.txt` propre à l'application, données
structurées `schema.org/Book`.

**La décision qui reste à prendre** : que faire des fiches épuisées. `RG-26` les maintient
au catalogue, et c'est non négociable — c'est le cas d'usage central des alertes. Mais cela
produit des milliers de pages à contenu très mince, le profil exact que les moteurs
déclassent, et qui peut entraîner le reste du domaine avec lui. Canonisation vers l'œuvre,
ou `noindex` sous un seuil de contenu. À trancher **avant la première indexation**.

**GA4 est un piège de copier-coller.** Le `Website` existant l'embarque, injecté au build
par `website-deploy.yml`. Le réflexe en créant une troisième application Angular sera de
reprendre la configuration — et de mettre l'association en défaut sur sa propre exigence
(`revue.md` `R-17`). La règle de `T-11` §7 s'applique : télémétrie sur la zone
d'administration, **jamais** sur les pages publiques.

**L'écran de désengorgement** (`F-05` §5) porte la requête la plus lourde du système. Elle
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
| Anti-répétition | `UserAlertHistory`, vérifiée **deux fois** — indicative à la clôture, faisant foi à l'envoi (`T-02` §2) |
| Personnes | Une seule table, `Watchlist` pour la facette membre (`DT-14`) |
| Rebonds | Rapports ACS via Event Grid (`T-07` §7) |

### Les points durs

**`R-06` — la suppression du compte dans le locataire — est en place côté code.** Le mécanisme
Graph, son enregistrement d'application et son secret ont été posés en `L0-11`. La PR #61
branche maintenant la cascade locale : liste de recherche, historique d'alertes, rebonds et
outbox d'alertes sont nettoyés avant suppression ou anonymisation, sans perdre les mouvements
retenus pour l'audit. Il reste à refaire le test de bout en bout sur un compte qui a réellement
vécu, puis à vérifier les deux côtés (locataire et base locale).

**Le repli de `RG-46`.** Si `QT-01` a montré une couverture insuffisante en `WorkId`, le
rapprochement par titre + auteur normalisés devient obligatoire. Il produit des faux
positifs sur les séries, les homonymes et les adaptations — retenu quand même, parce qu'un
membre prévenu à tort coûte moins cher qu'un membre jamais prévenu.

**L'ajout à la liste de recherche est l'écran le plus subtil du site** (`T-05` §4). Deux
erreurs à ne pas commettre : proposer l'édition avant l'œuvre — dans une bourse à 1–2 €,
la quasi-totalité des gens cherchent un texte, pas un tirage —, et masquer ensuite la
portée choisie.

**`QT-07` doit avoir été vérifiée au lot 0.** Si aucune configuration ne donne la connexion
seule, ouvrir l'inscription sur le catalogue ouvre aussi la création de comptes ailleurs.

### Les tests manuels qui comptent

- **Un cycle complet, de bout en bout, seul** : s'inscrire avec une adresse à soi, ajouter
  une œuvre à sa liste, scanner une édition de cette œuvre côté tri, clôturer la session, et
  **recevoir l'e-mail deux heures plus tard** — en boîte de réception, pas en indésirables.
  C'est le seul test qui éprouve la chaîne entière, et il n'a besoin de personne.
- **La fenêtre de rattrapage** : refaire la même chose, puis corriger la session dans le
  délai, et vérifier qu'aucun e-mail ne part.
- **La suppression de compte**, et vérifier des deux côtés : plus rien chez nous, plus rien
  dans le locataire.
- **Le rebond** : envoyer vers une adresse inexistante, et vérifier que `BounceCount`
  s'incrémente et que la suspension se déclenche au seuil.

---

## Ce qui reste ouvert après le palier 3

*Rien de structurel.*

Les deux manques que la revue signalait ici — le repli d'exploitation de `ENF-21` et la
stratégie de test des fronts — n'appartenaient à ce fichier que faute de mieux. Les deux
ont trouvé leur place ailleurs. Le repli d'exploitation **n'existera pas** — `ENF-21` est
réécrit : en cas de panne on vend sans enregistrer, et rien n'est rattrapé —, ce qui reporte
tout le poids sur le hors-ligne de la caisse, éprouvé en `P1-10`. La stratégie de test des
fronts se tranche en `P1-2`, avant l'application de scan et non après.

Restent les évolutions déjà identifiées et volontairement non planifiées, listées en
`F-01` §7 : estimation de la valeur marchande (`Q-02`), écran dédié de remise à plat de
l'inventaire, notifications push, application native pour le public, prise en charge des
livres sans ISBN.
