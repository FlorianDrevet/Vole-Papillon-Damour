# 05 — Le titre généré

## 1. Pourquoi il faut en générer un

Une publication Instagram ou Facebook n'a **pas de titre** : il n'y a qu'une légende.
Le site, lui, en a besoin partout — en `<h1>` de la fiche d'actualité, sur les cartes de
la page d'accueil, dans la liste par mois, dans le `alt` de l'image principale, et dans
les métadonnées de partage.

Les repli mécaniques ne tiennent pas : les premiers mots de la légende donnent des
titres tronqués au milieu d'un mot ou commençant par un emoji ; la date seule donne
autant de titres identiques que de publications dans le mois.

D'où le choix d'un petit modèle de langage, dont c'est exactement le format de tâche :
un texte court en entrée, une phrase courte en sortie, aucune connaissance externe
nécessaire.

## 2. Ce que le modèle a le droit de faire

**Résumer. Rien d'autre.**

| Autorisé | Interdit |
|---|---|
| Reprendre les mots de la légende | Ajouter un fait absent de la légende — une date, un lieu, un chiffre, un nom |
| Condenser une phrase longue | Interpréter, commenter, s'enthousiasmer |
| Mettre au format titre (majuscule initiale, pas de point final) | Produire un titre racoleur, une question, une exclamation |
| Renvoyer un titre en français | Traduire, changer de langue |

Le modèle **ne voit jamais les images**, seulement le texte. C'est un choix : décrire une
photo, c'est risquer de qualifier des personnes.

## 3. Contraintes de forme, vérifiées par le code

Le titre retourné est validé **avant** d'être écrit en base. Une seule violation
suffit à basculer sur le repli (`RG-ACT-20`).

| Contrainte | Valeur |
|---|---|
| Longueur | 15 à 70 caractères |
| Ligne | Une seule, aucun retour à la ligne |
| Ponctuation finale | Pas de point ; le point d'interrogation et d'exclamation sont refusés |
| Emoji et hashtags | Refusés |
| Guillemets englobants | Retirés silencieusement s'ils entourent tout le titre |
| Préfixes bavards | `Titre :`, `Voici`, `Proposition :` … retirés silencieusement |
| Langue | Français |

La validation est **mécanique** : c'est du code, pas une seconde requête au modèle.

## 4. Le repli

Si l'appel échoue, dépasse son délai, revient vide, ou ne passe pas la validation :

- Titre = **« Actualité du *j mois aaaa* »**, la date de la publication en toutes lettres
  en français.
- L'actualité est **créée quand même** (`RG-ACT-20`). Un titre médiocre relu en trente
  secondes vaut mieux qu'une publication perdue.
- Le brouillon porte le marqueur « titre à revoir » (`RG-ACT-21`).

Le repli n'est pas un cas exceptionnel à traiter plus tard : c'est le comportement
nominal quand le modèle n'est pas joignable, et il doit être testé comme tel.

## 5. Le service

| Sujet | Décision |
|---|---|
| Plateforme | **Azure AI Foundry**, dans l'abonnement Azure déjà utilisé par le site |
| Classe de modèle | Un modèle **« nano » ou « mini »** — la tâche est triviale, un grand modèle serait du gaspillage. `gpt-4.1-nano` est disponible en France Central et convient |
| Nom du déploiement | **Configuration**, jamais en dur dans le code. Changer de modèle ne doit pas demander de recompilation |
| Région | **France Central** ou zone de données UE, cohérent avec le reste de l'infrastructure et avec la résidence des données du projet |
| Authentification | **Identité managée** du Container App worker, rôle `Cognitive Services OpenAI User`. Aucune clé d'API, aucun secret de plus à faire tourner |
| Température | Basse (`0.2`) — on veut de la restitution, pas de la créativité |
| Jetons de sortie | Plafonnés (`~40`), ce qui coupe court à toute réponse bavarde |
| Délai d'attente | **10 secondes**, puis repli. Un import ne doit pas attendre un modèle |
| Filtres de contenu | Ceux d'Azure par défaut. Un blocage est traité comme un échec ordinaire → repli |

## 6. Ce qui est envoyé, et ce qui ne l'est pas

**Envoyé :** le texte nettoyé de la légende (`RG-ACT-07`), tronqué à 2 000 caractères.

**Jamais envoyé :** les images, l'identifiant du compte, l'identifiant de la publication,
et toute donnée du site — adhérents, catalogue, commandes.

La légende est un texte que l'association a **déjà publié publiquement**. Elle peut
néanmoins contenir des noms de personnes. C'est le point à retenir pour l'analyse RGPD
(`ENF-ACT-10`, `ENF-ACT-12`) : le traitement se limite à un résumé, sans conservation par le
fournisseur au-delà de la requête, sans entraînement sur les données, et sans autre
destinataire.

**Aucune sortie du modèle n'est traitée comme une instruction.** Une légende
malveillante — hypothèse improbable ici, mais gratuite à couvrir — ne peut au pire que
produire un titre refusé par la validation de la section 3.

## 7. Coût

Une publication ≈ 600 jetons d'entrée et 20 de sortie. Au rythme de l'association —
quelques publications par mois — le coût annuel se compte en **centimes**. Le sujet ne
mérite pas de surveillance particulière ; en revanche le plafond de `RG-ACT-17` évite
qu'une boucle d'erreur ne transforme des centimes en facture.

## 8. Comment on saura que ça marche

Il n'y a pas de mesure automatique de la qualité d'un titre, et ce n'est pas grave :
l'administrateur relit de toute façon.

La mesure utile est **le taux de titres conservés tels quels**, observable à la main sur
les dix premières actualités importées :

- si la plupart des titres sont gardés → le dispositif remplit son office ;
- s'ils sont systématiquement réécrits → l'instruction du modèle est à revoir, ou la
  fonctionnalité ne vaut pas son coût et le repli par date suffit.

C'est un arbitrage à prendre **après** avoir vu de vrais titres sur de vrais contenus,
pas avant.

## Sources

- [Foundry Models sold by Azure — Microsoft Learn](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure) — catalogue des modèles disponibles
- [Region availability for Foundry Models — Microsoft Learn](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability) — disponibilité régionale, dont France Central
