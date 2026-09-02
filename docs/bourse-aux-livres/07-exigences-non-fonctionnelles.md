# 07 — Exigences non fonctionnelles

## Réactivité du scan

### `ENF-01` — Délai d'affichage du verdict
Entre la lecture du code-barres et l'affichage du verdict, **une seconde au maximum**,
réseau nominal.

Ce n'est pas un confort. Un bénévole qui attend deux secondes par livre perd plus d'une
demi-heure sur mille livres, et cessera d'utiliser l'application.

### `ENF-02` — Pas d'attente sur les sources externes
La consultation d'une source de métadonnées ou de valeur ne doit jamais bloquer
l'affichage. Une information indisponible dans le délai s'affiche en différé ou pas du
tout ; le verdict de doublon et de vente, lui, provient des données de l'association et
est toujours disponible.

### `ENF-03` — Cadence soutenue
L'application doit tenir un scan toutes les deux secondes pendant une heure sans
dégradation ni fuite de mémoire.

### `ENF-04` — Autonomie
Une session de tri complète doit tenir sur une charge de l'appareil. À vérifier sur le
matériel réel avant tout achat en nombre.

---

## Fonctionnement dégradé

### `ENF-05` — Mode hors-ligne au tri et en caisse
Le local et la salle de bourse peuvent être mal couverts. L'application doit :

- continuer à scanner et à enregistrer les gestes sans réseau,
- afficher les verdicts à partir des données locales, **en signalant explicitement leur
  date de fraîcheur**,
- conserver les gestes en attente à travers une fermeture de l'application ou une
  coupure de batterie,
- synchroniser automatiquement au retour du réseau.

### `ENF-06` — Résolution des conflits
Les gestes sont des mouvements indépendants et cumulatifs : deux appareils ayant scanné
le même ISBN hors-ligne produisent deux mouvements, pas un conflit. Aucune fusion
manuelle ne doit être demandée à un bénévole.

### `ENF-07` — Visibilité de l'état de synchronisation
Le nombre de gestes en attente est visible en permanence. Un bénévole ne doit jamais
ranger un appareil en croyant son travail enregistré alors qu'il ne l'est pas.

---

## Site public

### `ENF-08` — Temps de réponse de la recherche
Résultats en moins d'une seconde sur un catalogue de 15 000 titres.

### `ENF-09` — Usage mobile et référencement
La majorité des visites viendra du téléphone. Les fiches livres doivent être indexables
par les moteurs de recherche : c'est le principal canal d'acquisition gratuit pour
l'association, et il vaut plus que n'importe quelle campagne de communication.

---

## Données personnelles

### `ENF-10` — Minimisation
Seules sont collectées l'adresse e-mail et la liste de recherche. Ni nom, ni adresse
postale, ni téléphone, ni date de naissance.

### `ENF-11` — Information et consentement
La finalité est annoncée au moment de l'inscription, en clair, et pas seulement dans
les mentions légales. Aucune case pré-cochée.

### `ENF-12` — Droit à l'effacement
La suppression du compte est accessible en deux clics depuis « Mon compte » et supprime
effectivement la liste de recherche et l'historique d'alertes. Les mouvements de vente,
qui ne contiennent aucune donnée personnelle, sont conservés.

### `ENF-13` — Conservation
Un compte inactif depuis trois ans est supprimé après une relance par e-mail.

### `ENF-14` — Absence de cession et de traçage
Aucune adresse e-mail n'est transmise à un tiers. Aucun traceur publicitaire sur le site
public. Les statistiques de fréquentation, si elles existent, doivent fonctionner sans
consentement — c'est-à-dire sans bandeau de cookies.

### `ENF-15` — Données des bénévoles
L'activité individuelle des bénévoles est visible des administrateurs pour la
correction d'erreurs et le suivi d'activité. Elle ne doit servir à aucune forme de
classement ou de comparaison entre bénévoles.

---

## Authentification

### `ENF-26` — Un seul fournisseur d'identité
**Microsoft Entra External ID pour tous les publics**, sans exception : membres du site,
bénévoles, administrateurs. L'association ne stocke ni ne gère aucun mot de passe, nulle
part. L'authentification maison du backend est **supprimée** — voir `DT-10`.

C'est l'exigence dont les trois suivantes découlent. Elle prime sur elles en cas de
doute.

### `ENF-16` — Membres du public
Compte créé **en libre-service**, proposé seulement au clic sur « me prévenir » (`04`
§6), jamais à l'entrée du site. Un membre inscrit n'a **aucun droit particulier** :
son statut est l'absence de rôle.

### `ENF-17` — Bénévoles
Comptes individuels **créés par un administrateur** ; aucune inscription en libre-service
sur l'application de scan. Session longue sur l'appareil : la reconnexion à chaque
session de tri est exclue.

Cette dernière phrase entre en tension avec la durée de vie des jetons du fournisseur
d'identité. La tension est réelle, elle est traitée en `10` §9 et mesurée par `QT-08` :
l'identité du bénévole est lue sur l'appareil, le jeton ne sert qu'à synchroniser.

### `ENF-18` — Administrateurs
Même fournisseur d'identité que les membres, **distingués par un rôle applicatif
explicite** — `Administration` — et non par un mécanisme d'authentification séparé. Les
droits de tri et de caisse suivent la même forme (`RG-40`).

### `ENF-27` — Configuration scriptée
Toute configuration du locataire d'identité se fait **par script** (`infra/entra/`),
jamais à la main dans le portail : enregistrements d'application, rôles, attribution des
droits. Deux exceptions assumées et documentées : la création du locataire, et le flux
d'inscription en libre-service tant que son API reste en préversion.

---

## Accessibilité

### `ENF-19` — Application de scan
Contraste élevé, typographie large, cibles tactiles généreuses. Les verdicts ne
reposent **jamais sur la seule couleur** : chaque verdict porte un libellé écrit et un
pictogramme. Une part des bénévoles est âgée et le local est parfois mal éclairé.

### `ENF-20` — Site public
Navigation au clavier, contrastes conformes, textes alternatifs sur les couvertures,
structure de titres cohérente.

---

## Exploitation

### `ENF-21` — Continuité de la vente
Une indisponibilité du système ne doit jamais empêcher de vendre. La caisse reste
physique : l'argent rentre, le livre part, **et rien n'est enregistré**.

**Aucun repli n'est prévu, et c'est une décision.** Pas de feuille de papier, pas d'écran
de ressaisie, aucune procédure de rattrapage. Une panne un jour de bourse produit des
ventes que le système ne verra jamais, et l'écart qui en résulte se résorbe à la prochaine
remise à plat (`RG-34`), au même titre qu'un livre vendu sans scan.

*Pourquoi ne rien prévoir.* Un dispositif de secours a un coût permanent — l'écrire, le
tenir à jour, l'expliquer à des bénévoles qui ne s'en serviront peut-être jamais — pour un
bénéfice qui ne se manifeste qu'en cas de panne, et qui ne serait de toute façon qu'une
correction de stock. Le système ne tient pas la caisse : il ne connaît ni encaissement, ni
recette, ni monnaie. Ce qu'une panne fait perdre est une **information de stock**, pas de
l'argent.

*Ce que cela accepte.* La dérive de `Q-04` s'aggrave à chaque incident, et l'écart mesuré
au comptage mélange alors deux causes — la discipline de scan et l'indisponibilité. C'est
tenable tant que les pannes sont rares ; si elles ne le sont pas, c'est la fiabilité qu'il
faut corriger, pas le repli qu'il faut écrire.

*Ce qui reste exigé, en revanche.* `ENF-05` — le fonctionnement hors ligne de la caisse —
n'est pas affaibli par cette décision : il en devient la seule protection réelle. Une
coupure réseau ne doit pas être une panne.

### `ENF-22` — Sauvegardes
Les mouvements constituent l'historique comptable de l'activité. Ils doivent être
sauvegardés au même niveau d'exigence que les données existantes de l'association.

### `ENF-23` — Coût d'hébergement
Le projet doit rester dans la continuité de l'hébergement Azure actuel. Toute
dépendance payante — en particulier une source de valeur marchande, voir `Q-02` — doit
être chiffrée et validée avant adoption.

### `ENF-24` — Maintenabilité
Le projet est développé et maintenu bénévolement, par une seule personne le plus
souvent. Ce critère prime sur la sophistication : une fonctionnalité qui ne peut pas
être maintenue dans ces conditions ne doit pas être construite.

### `ENF-25` — Paramétrage sans redéploiement
Tous les seuils listés en `05` §9 sont modifiables par un administrateur sans
intervention technique.
