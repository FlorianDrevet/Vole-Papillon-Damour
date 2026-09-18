# Habillage des e-mails de l'association

## Logo dans le contenu du message

Les alertes de livres utilisent `BookAlerts:Email:LogoUrl`. Le Worker ajoute cette
image en première position dans le HTML du message, dans une table compatible avec
les clients de messagerie et avec un texte alternatif portant le nom de l'association.

La valeur de développement est :

```text
https://volepapillondamour.fr/icons/vpd_icon.png
```

Cette image est visible dans le message lorsque le client autorise l'affichage des
images distantes. Elle ne contrôle pas l'avatar affiché à gauche dans la liste de
réception.

Une version vectorisée carrée destinée à BIMI est également livrée dans
`src/Website/public/icons/vpd_bimi.svg`. Après déploiement du Website, elle sera
accessible à l'adresse :

```text
https://volepapillondamour.fr/icons/vpd_bimi.svg
```

La vectorisation est dérivée du PNG existant et a été contrôlée localement : XML
valide, `baseProfile="tiny-ps"`, moins de 32 Ko, sans balise `image`, script ni
référence externe. Une validation par un fournisseur BIMI reste recommandée avant
publication DNS.

## Logo à côté de l'expéditeur — BIMI

Le logo de la liste de réception dépend du fournisseur de boîte mail. La procédure
BIMI à poursuivre pour l'adresse actuelle `DoNotReply@mail.volepapillondamour.fr`
est la suivante :

1. Préparer une version officielle du papillon en **SVG Tiny P/S**, carrée, sans
   script, sans ressource externe et servie avec `image/svg+xml`.
2. Héberger ce SVG sur une URL HTTPS stable du domaine de l'association.
3. Vérifier que SPF et DKIM sont alignés avec `mail.volepapillondamour.fr`.
4. Le DNS observé le 2026-09-18 publie déjà `_dmarc.mail.volepapillondamour.fr`
   en `p=quarantine`. Après contrôle des rapports DMARC, le passage à `p=reject`
   pourra être décidé séparément.
5. Publier ensuite le TXT BIMI suivant, en remplaçant les URL par les fichiers réels :

   ```text
   Nom : default._bimi.mail
   Type : TXT
   Valeur : v=BIMI1; l=https://volepapillondamour.fr/icons/vpd_bimi.svg; a=https://<domaine-public>/chemin/vpd-bimi.pem
   ```

Le fichier `.pem` est le certificat VMC/CMC lorsqu'il est requis par le fournisseur.
Gmail demande une certification de marque pour son affichage vérifié ; les autres
fournisseurs peuvent appliquer leurs propres conditions. Aucun enregistrement DNS
BIMI fictif n'est ajouté au dépôt tant que le SVG officiel et le certificat ne sont
pas disponibles.

La zone DNS est gérée hors Azure, chez OVH. La publication DMARC/BIMI et l'achat
éventuel du certificat doivent donc être effectués et vérifiés séparément après la
fusion de la modification applicative.
