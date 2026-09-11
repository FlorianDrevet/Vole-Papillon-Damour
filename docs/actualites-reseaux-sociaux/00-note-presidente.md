# 00 — Note à la présidente

> Cette note se lit seule. Les autres documents du dossier sont techniques.

## Ce qu'on veut faire

Vous publiez déjà les nouvelles de l'association sur Facebook, et vous cochez la case
qui les envoie aussi sur Instagram. Ensuite, quelqu'un recopie la même chose dans le
back-office pour que ça apparaisse sur le site. C'est deux fois le même travail.

L'idée : **le site va chercher tout seul votre publication**, récupère le texte et les
photos, et prépare l'actualité. Vous n'écrivez plus qu'une fois.

## Ce qui ne change pas

**Rien ne part en ligne sans qu'une personne ait dit oui.** L'actualité préparée
automatiquement arrive dans le back-office à l'état de **brouillon**. Tant que personne
ne l'a relue et validée, elle n'est visible nulle part sur le site.

C'est volontaire, pour trois raisons :

- Une publication Facebook parle à des gens qui vous suivent déjà ; une actualité du
  site s'adresse à des inconnus. Le ton n'est pas toujours le même.
- Les photos où l'on reconnaît des gens, et surtout des enfants, méritent qu'on
  s'arrête une seconde avant de les mettre sur un site public consultable par tous.
- Le titre est fabriqué par une intelligence artificielle (voir plus bas). Il est
  généralement bon, mais il faut pouvoir le corriger.

Vous continuez donc à publier exactement comme aujourd'hui. Le seul geste nouveau, c'est
une relecture dans le back-office, qui prend quelques secondes au lieu d'une ressaisie
complète.

## Le titre

Facebook et Instagram n'ont pas de titre : il n'y a qu'un texte. Le site, lui, en a
besoin, en haut de chaque actualité. On demande donc à un petit programme d'intelligence
artificielle de proposer un titre à partir de votre texte — une phrase courte, en
français, qui reprend ce que vous avez écrit.

Ce titre est **une proposition**. Il apparaît dans le brouillon, vous pouvez le
réécrire entièrement. Le programme n'a pas le droit d'inventer une information qui n'est
pas dans votre texte : il ne fait que résumer.

## Les deux décisions qu'on ne peut pas prendre à votre place

### 1. Le compte Instagram doit être un compte « professionnel »

Instagram distingue les comptes personnels et les comptes professionnels (aussi appelés
« créateur » ou « entreprise »). **Seuls les comptes professionnels peuvent être lus
automatiquement.** C'est une règle de Meta, pas un choix de notre part.

Si `vole_papillon_damour` est encore un compte personnel, il faut le basculer : c'est
gratuit, réversible, et ça se fait dans les réglages de l'application en trois écrans.
Ça ne change rien à ce que voient vos abonnés.

**Sans cette bascule, la fonctionnalité ne peut pas exister.**

### 2. Faut-il créer une page Facebook pour l'association ?

Aujourd'hui, les nouvelles partent de **votre profil personnel**
(`facebook.com/melvin.drevet.1`). C'est ce profil qui est affiché comme « le Facebook de
l'association » sur tout le site.

Deux problèmes, indépendants de ce projet :

- Meta **n'autorise pas** un site à lire automatiquement les publications d'un profil
  personnel. C'est réservé aux pages. C'est pour ça que le site passera par Instagram et
  pas par Facebook.
- Le jour où vous ne serez plus présidente, la présence Facebook de l'association part
  avec votre compte.

Créer une **page Facebook** au nom de l'association réglerait les deux : le site
pourrait alors être prévenu **au moment même** de la publication, au lieu d'aller
regarder toutes les demi-heures, et la page appartiendrait à l'association, pas à une
personne.

Ce n'est **pas obligatoire** pour démarrer. On peut très bien commencer par Instagram
seul. Mais c'est la bonne cible, et il vaut mieux le décider maintenant que dans deux
ans.

## Ce que ça coûte

Presque rien. La lecture des publications est gratuite chez Meta. Le titre généré coûte
une fraction de centime par actualité — quelques centimes par an au rythme de
l'association. Le reste tourne sur des ressources Azure déjà payées pour le site.

## Ce qui peut mal se passer, et ce qu'on prévoit

| Ce qui peut arriver | Ce qui se passe alors |
|---|---|
| Meta change ses règles ou coupe l'accès | Le site continue de fonctionner ; on revient à la saisie manuelle, qui n'est jamais retirée |
| L'autorisation d'accès expire (tous les deux mois) | Une alerte est envoyée **avant** l'expiration ; sans renouvellement, l'import s'arrête, le site reste intact |
| Le titre proposé est mauvais | Vous le réécrivez dans le brouillon, comme aujourd'hui |
| Une photo ne doit pas aller sur le site | Vous la retirez du brouillon, ou vous supprimez le brouillon |

Rien de tout cela ne peut casser le site : l'import ne fait qu'ajouter des brouillons.
