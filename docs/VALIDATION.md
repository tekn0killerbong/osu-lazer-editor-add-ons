# Validation — EditorAddons 1.0.0

Publication **osu-editor-add-ons v1.0**, préparée le 24 novembre 2026. Cible : **osu!lazer 2026.916.0**, SDK **.NET 10.0.404**, Linux x64.

## Vérifications exécutées

- Restauration verrouillée et compilation Release de la solution `EditorAddons.sln` : **0 erreur**.
- **53 vérifications de logique/API** : outils des éditeurs, bindings, contrôles de grille, migration des raccourcis et absence de patches des skins Catch.
- **6 scénarios de courbes aller-retour**, plus les vérifications de refus et d’absence de patch du processeur natif de positions.
- **8 vérifications de chargement à froid**, dans un processus sans référence préalable à l’extension ni à Harmony. La DLL renommée est inspectée avec `GetTypes` avant construction du carrier ; ses patches s’installent et Harmony est chargé depuis sa ressource embarquée, sans DLL Harmony adjacente.
- Lecture des métadonnées du binaire : assembly `osu.Game.Rulesets.EditorAddons`, version d’assembly et de fichier `1.0.0.0`, version de produit `1.0.0`.
- Contrôle du paquet public : une seule DLL d’extension ; seuls les sources et tests d’EditorAddons et le banc indépendant de découverte sont inclus.

## Renommage et compatibilité des réglages

Le namespace, le projet, le carrier, le nom d’assembly et la ressource Harmony utilisent désormais EditorAddons. La logique d’édition reste celle d’EditorTools. Les identifiants des raccourcis, le carrier interne `editor-tools`, l’identifiant des patches et les fichiers de préférences historiques sont conservés pour réutiliser les données existantes.

Il faut retirer `osu.Game.Rulesets.EditorTools.dll` du dossier `rulesets` lors de l’installation de la DLL renommée. Charger simultanément l’ancienne et la nouvelle version n’est pas pris en charge.

## Limites

Les vérifications de cette révision sont exécutées sous Linux, sans rendu GPU. Les essais complets de l’éditeur avec souris/clavier et de lecture audio n’ont pas été rejoués. Le script PowerShell et le workflow GitHub Windows/Linux restent à exécuter sur leurs environnements respectifs.

La compilation complète conserve 3 avertissements de nullabilité `CS8600` dans `EditorPlayback.cs`. NuGet signale aussi des vulnérabilités modérées ou élevées dans des dépendances transitives de la version ciblée du jeu : AutoMapper 13.0.1, MessagePack 3.1.8, Microsoft.AspNetCore.SignalR.Common 10.0.12, Microsoft.AspNetCore.SignalR.Protocols.MessagePack 10.0.12 et SixLabors.ImageSharp 3.1.11. Les avertissements ne sont pas désactivés. Ces composants restent verrouillés pour la compatibilité ; leurs DLL ne sont pas redistribuées avec l’extension et proviennent de l’installation de lazer.

La compatibilité avec une autre version du client doit être vérifiée après changement de ses API internes.
