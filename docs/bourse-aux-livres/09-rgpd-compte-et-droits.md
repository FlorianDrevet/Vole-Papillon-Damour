# 09 — RGPD : compte, droits et transparence

> Étude opérationnelle rédigée le 12 septembre 2026 pour le Catalogue public. Ce document
> aide l'association à préparer la mise en production ; il ne constitue pas un avis
> juridique. L'association doit valider les bases juridiques, les durées, les contrats
> fournisseurs et l'éventuelle obligation de désigner un DPO avec son conseil.

## Conclusion pratique

L'ajout d'un compte crée une collecte de données personnelles. Le Catalogue doit donc
donner une information courte au moment où la personne crée son compte, conserver une
politique de confidentialité complète, proposer un lien public facilement trouvable et
permettre l'exercice effectif des droits.

La livraison ajoute :

- la page publique `/donnees-personnelles`, reliée au footer du Catalogue ;
- un premier niveau d'information dans la carte « Créer un compte » ;
- un parcours e-mail pour demander l'accès, la rectification, l'effacement, la limitation,
  l'opposition ou la portabilité ;
- le rappel du délai d'un mois, de la vérification d'identité proportionnée et du recours
  auprès de la CNIL ;
- une formulation cohérente avec la suppression locale et la suppression Entra différée.

## 1. Responsable et périmètre

Pour le compte, la liste de recherche et les alertes du Catalogue, le responsable du
traitement est l'association **Vole Papillon d'Amour** :

- 46 route de Saint Marcellin, 42170 Saint-Just-Saint-Rambert, France ;
- `volepapillondamour@sfr.fr`.

Le périmètre est le site `https://livres.volepapillondamour.fr` et son API. Le site de
l'association, Microsoft Entra External ID, Azure Communication Services, les outils de
mesure et Google Maps peuvent avoir leurs propres informations ou responsabilités ; les
contrats et rôles de chaque fournisseur doivent être vérifiés.

## 2. Traitements constatés dans le code

| Traitement | Données principales | Finalité | Base à valider par l'association |
|---|---|---|---|
| Consultation publique | Données techniques éventuelles des requêtes et journaux | Fonctionnement, sécurité, diagnostic | Intérêt légitime, avec durée et accès documentés |
| Compte et connexion | Adresse e-mail, prénom/nom lorsqu'ils sont renseignés, identifiant externe, dates de compte et de dernière activité ; aucun mot de passe dans le Catalogue/API | Créer et maintenir l'espace membre | Exécution du service demandé / mesures précontractuelles, à confirmer |
| Liste de recherche | ISBN ou œuvre suivie, dates d'ajout et de dernière alerte | Fournir le suivi choisi | Exécution du service demandé |
| Alertes | Préférence, historique d'alerte, état de remise et rebonds techniques | Envoyer l'information demandée et éviter les envois en échec | Exécution du service demandé ; absence de prospection, à maintenir |
| Audience et carte | Données d'usage ou données techniques transmises au tiers | Mesurer l'usage ou afficher Maps | Consentement préalable et distinct |
| Effacement | Données locales, identité Entra, éventuel mouvement métier conservé | Répondre à la demande d'effacement tout en préservant une trace légalement nécessaire | Effacement ; obligation légale/intérêt légitime uniquement pour le résiduel nécessaire |

Le registre des traitements doit reprendre ce tableau avec les champs exacts, les
catégories de personnes, les destinataires, les lieux de traitement, les durées et les
mesures de sécurité.

## 3. Information à donner au moment de la collecte

Le premier niveau d'information doit être visible avant ou pendant la création du compte,
avec des mots simples et un lien vers la page complète. Il doit au minimum indiquer :

1. l'identité et les coordonnées du responsable du traitement ;
2. les finalités : connexion, espace personnel, liste de recherche et alertes demandées ;
3. les catégories de données et la distinction entre ce qui est nécessaire et ce qui est
   facultatif ;
4. les conséquences d'un refus ou de l'absence de données facultatives ;
5. les destinataires ou catégories de destinataires, notamment Entra et le service
   d'envoi lorsque l'alerte est utilisée ;
6. la durée de conservation ou les critères utilisés pour la déterminer ;
7. les droits et leur mode d'exercice, ainsi que le droit de saisir la CNIL ;
8. les transferts éventuels hors UE et les garanties applicables ;
9. l'existence ou non d'une décision automatisée produisant un effet juridique ou
   significatif — le Catalogue n'en met pas en œuvre pour le compte membre.

Le consentement aux cookies, à la mesure d'audience et à Google Maps ne doit pas être
présenté comme une condition de création du compte. Le lien « Gérer les cookies » permet
de retirer un consentement à tout moment.

## 4. Exercice des droits

### Accès et copie

La personne peut écrire à l'adresse de contact avec l'objet « Demande RGPD — mes
données » ou utiliser le bouton de la page. Elle doit préciser le droit exercé, l'adresse
e-mail utilisée pour le compte et, si possible, le périmètre souhaité. Une demande
d'accès doit permettre de fournir une copie des données et les informations associées
dans un format électronique courant.

Il ne faut pas demander systématiquement une pièce d'identité au premier message. Une
vérification complémentaire n'est justifiée qu'en cas de doute raisonnable et doit être
proportionnée, transmise par un moyen sécurisé et limitée à ce qui est nécessaire.

### Autres droits

- **Rectification** d'une donnée inexacte ou incomplète ;
- **Effacement** quand aucune obligation ou exception ne justifie la conservation ;
- **Limitation** pendant une contestation ou une vérification ;
- **Opposition** à un traitement fondé sur l'intérêt légitime, pour des raisons liées à
  la situation de la personne ;
- **Portabilité** uniquement lorsque les conditions légales sont réunies : données
  fournies par la personne, traitement automatisé fondé sur le consentement ou le
  contrat, et respect des droits des tiers ;
- **Retrait du consentement** pour les traitements fondés sur celui-ci, sans remettre en
  cause les traitements antérieurs licites.

### Délais et réponse

La réponse est gratuite en principe et doit être apportée dans le mois. Une prolongation
de deux mois est possible lorsque la demande est complexe ou nombreuse, à condition d'en
informer la personne dans le premier mois et d'en expliquer la raison. En cas de refus ou
d'absence de suite, la réponse doit expliquer les motifs et rappeler le droit de saisir
la CNIL.

## 5. Suppression du compte dans l'application

Le parcours `/compte` propose une double confirmation. La demande actuelle :

1. met en file une demande durable ;
2. demande la suppression de l'identité dans Microsoft Entra External ID ;
3. supprime le profil local, la liste, l'historique d'alertes, les rebonds et les alertes
   en attente ;
4. supprime la ligne locale si aucune trace métier ne doit la conserver ;
5. anonymise la ligne si une trace métier légalement nécessaire doit rester liée à un
   mouvement, sans conserver l'e-mail, le nom ou l'identifiant externe exploitables.

La réponse HTTP peut donc être acceptée avant la fin du traitement différé. Le texte du
compte et la page publique ne doivent pas promettre une suppression instantanée si le
fournisseur d'identité ou le worker est momentanément indisponible.

## 6. Gaps à fermer avant mise en production

### Durées de conservation

La propriété `LastSeenAt` existe et la spécification vise une purge après trois ans
d'inactivité avec relance, mais aucun parcours automatique de purge d'inactivité n'a été
constaté dans le worker actuel. Il faut choisir puis documenter :

- la durée du compte actif et le point de départ de l'inactivité ;
- la relance, son contenu et sa preuve d'envoi ;
- le délai de suppression après relance ;
- la durée des journaux, des événements de remise et des données d'audience ;
- les éventuelles durées imposées par une obligation comptable ou contentieuse.

Tant que ces valeurs ne sont pas décidées et réellement appliquées, la page publique
reste volontairement sur des critères de conservation et n'annonce pas « trois ans » comme
un comportement déjà garanti.

### Fournisseurs et transferts

L'association doit conserver les DPA/clauses contractuelles et vérifier les lieux et
sous-traitants pour Microsoft Entra External ID, Azure SQL/Container Apps/Application
Insights, Azure Communication Services, Clarity, GA4 et Google Maps. Tout transfert hors
UE doit reposer sur une décision d'adéquation ou des garanties appropriées, avec
l'information correspondante dans la politique.

### Gouvernance

Avant l'ouverture publique, l'association doit :

- valider les bases juridiques de chaque ligne du registre ;
- déterminer si un DPO est obligatoire ou volontairement désigné, et publier ses
  coordonnées le cas échéant ;
- documenter les habilitations, le chiffrement, les journaux d'accès et la procédure de
  violation de données ;
- tester une demande d'accès et une demande d'effacement de bout en bout, y compris un
  échec/rejeu Graph et l'absence d'alerte résiduelle ;
- vérifier que l'e-mail d'alerte reste strictement lié au service demandé et ne devient
  pas une campagne commerciale sans base et information adaptées ;
- réviser cette page dès qu'un champ, fournisseur, transfert, durée ou fonctionnement de
  suppression change.

## Références officielles

- [CNIL — Informer les personnes et assurer la transparence](https://www.cnil.fr/fr/conformite-rgpd-information-des-personnes-et-transparence)
- [CNIL — Répondre à une demande de droit d'accès](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces)
- [CNIL — Préparer l'exercice des droits](https://www.cnil.fr/fr/preparer-lexercice-des-droits-des-personnes)
- [CNIL — Déposer une plainte](https://www.cnil.fr/fr/plaintes)
- [Règlement (UE) 2016/679, texte consolidé — articles 12 à 21](https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=fr)
- [CNIL — Règles applicables aux cookies et autres traceurs](https://www.cnil.fr/fr/cookies-et-autres-traceurs/regles/cookies/lignes-directrices-modificatives-et-recommandation)
