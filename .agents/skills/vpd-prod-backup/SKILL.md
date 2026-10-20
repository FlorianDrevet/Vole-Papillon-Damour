---
name: vpd-prod-backup
description: "Back up or restore the Vole-Papillon-Damour production Azure SQL database and Blob assets when explicitly requested. Exports a portable copy to a dated Desktop folder; do not use for resets or deployments."
---

# Sauvegarde locale Azure de VPD

Utiliser ce skill quand l'utilisateur demande explicitement une sauvegarde locale ou la restauration d'une sauvegarde Vole-Papillon-Damour.

## Environnement à traiter comme production

L'utilisateur a confirmé que ses ressources Azure dont le nom contient `dev` sont son environnement de production. Les cibles vérifiées dans le dépôt sont :

- Groupe de ressources : `rg-vpd-dev`
- Serveur Azure SQL : `vpd-sql-dev`
- Base : `vole-papillon-damour-db`
- Storage Account Blob : `vpdstdevfc`

Ne jamais sélectionner un workflow de reset ou déduire du nom `dev` que les données sont jetables. Le skill de remise à zéro des livres est distinct et ne fait pas partie de cette sauvegarde.

## Créer une sauvegarde

Sur demande explicite de sauvegarde, exécuter `scripts/export-vpd-prod-backup.ps1` avec PowerShell 7 depuis la machine de l'utilisateur, pas dans GitHub Actions ni dans un conteneur éphémère. Le script :

1. Vérifie la session Azure CLI, la subscription active, les ressources exactes et l'accès lecture Blob. Si `SqlPackage` manque, installe-le comme outil .NET dans `%LOCALAPPDATA%\VpdBackup\SqlPackage` sans modifier le `PATH` global.
2. Crée `Bureau\VPD-Sauvegarde-Production-<date>-<id>` avec un sous-dossier SQL et un sous-dossier pour chaque conteneur Blob.
3. Demande une copie PITR temporaire à l'heure UTC située environ 15 minutes avant le lancement, pour exporter une copie SQL cohérente sans bloquer les écritures de l'application.
4. Ajoute une règle de pare-feu Azure SQL limitée à l'adresse IPv4 publique courante, exporte la copie temporaire en `.bacpac` avec l'authentification Microsoft Entra, puis supprime la règle et la base temporaire dans `finally`.
5. Télécharge tous les conteneurs et blobs de `vpdstdevfc` avec l'identité Azure CLI de l'utilisateur, exporte aussi les propriétés/métadonnées Blob, puis écrit un manifeste, un guide de restauration et les empreintes SHA-256.

Ne jamais afficher, enregistrer ou demander de coller un mot de passe SQL, une chaîne de connexion, un jeton ou une clé Storage. Si l'identité Microsoft Entra n'a pas les droits nécessaires, arrêter et vérifier que les ressources temporaires ont été supprimées; ne pas contourner l'échec avec un mot de passe stocké dans le dépôt.

Le `.bacpac` contient des données non chiffrées. Garder le dossier sur un volume protégé par BitLocker et ne jamais le committer, joindre à une PR ou publier. Un BACPAC est une archive portable; pour une restauration rapide à une heure précise, les sauvegardes natives PITR/LTR Azure restent le premier choix.

Si la restauration ou l'export échoue après la création des ressources temporaires, réessayer leur nettoyage une fois et afficher les noms exacts de toute ressource qui n'a pas pu être supprimée. Ne pas lancer de nettoyage large ni supprimer une ressource préexistante.

## Restaurer une sauvegarde

Lorsqu'une restauration est demandée, lire le `README-RESTAURATION.md` et le `manifest.json` du dossier choisi, vérifier les fichiers et leurs empreintes, puis clarifier la destination si elle n'est pas indiquée.

- Pour retrouver la base à un instant récent, privilégier une restauration PITR/LTR Azure vers une **nouvelle base**.
- Pour importer le `.bacpac`, utiliser `SqlPackage /Action:Import` vers une base portant un nouveau nom. Ne jamais importer dans la base de production existante.
- Restaurer les fichiers Blob vers un compte/conteneurs de destination séparés et réappliquer les propriétés indiquées dans `blob-metadata`. Ne pas écraser ni supprimer les blobs de production pendant cette étape.
- Vérifier la base restaurée, ses tables et les fichiers Blob avant tout basculement de l'application. Un changement de connexion ou une substitution de la base en production nécessite une demande explicite distincte.

Ne pas exécuter une sauvegarde pendant une simple demande d'explication, de création du skill ou de revue du code.
