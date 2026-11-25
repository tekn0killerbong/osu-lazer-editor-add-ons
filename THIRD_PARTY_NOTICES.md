# Notices des composants tiers

Les restrictions non commerciales de `LICENSE.md` portent sur les contributions originales du projet. Les droits indépendants accordés par les licences ci-dessous sont conservés pour leurs composants respectifs.

| Composant | Version / provenance | Présence dans la livraison | Licence |
| --- | --- | --- | --- |
| osu! / ppy | API NuGet 2026.916.0 ; sources JuiceStreamPlacementBlueprint et JuiceStream | `FreehandJuicePlacement.cs` est adapté de la classe officielle ; l’échantillonnage de `CurveBaker.cs` suit la génération native des gouttes. Les DLL du jeu sont fournies par lazer, pas par ce paquet. | MIT, `LICENSES/osu-MIT.txt` |
| Harmony | Lib.Harmony 2.4.3, commit b79f2f1ac971e5bc224cda3efedbbb4084efbfb6 | `0Harmony.dll` embarqué comme ressource dans la DLL EditorAddons | MIT, `LICENSES/Harmony-MIT.txt` |
| MonoMod.Core, Utils, Backports, ILHelpers | 1.3.6 / 25.0.14 / 1.1.2 / 1.1.0, dépendances de Harmony | Inclus dans la distribution fusionnée de Harmony | MIT, `LICENSES/MonoMod-MIT.txt` |
| Mono.Cecil | 0.11.6, dépendance de MonoMod | Distribution fusionnée de Harmony | MIT, `LICENSES/Mono.Cecil-MIT.txt` |
| Iced | Notice livrée dans le paquet MonoMod.Core 1.3.6 | Code utilisé par MonoMod | MIT, `LICENSES/Iced-MIT.txt` |

Sources : https://github.com/ppy/osu ; https://github.com/pardeike/Harmony ; https://github.com/MonoMod/MonoMod ; https://github.com/jbevain/cecil ; https://github.com/icedland/iced.

L’en-tête hérité de `FreehandJuicePlacement.cs` renvoie au fichier LICENCE du dépôt osu! original ; son texte est reproduit ici dans `LICENSES/osu-MIT.txt`. Les sources Shred fournies par l’auteur ont servi de référence au comportement et au panneau de contrôle. Les contrôles PlaybackRateControl/PlaybackPitchControl et leur intégration éditeur/Test ont été transférés et adaptés à cette extension à sa demande. Aucun binaire Shred ni référence d’assembly Shred n’est incorporé.

Les autres dépendances NuGet des projets servent à compiler et à exécuter le banc d’essai. Leurs binaires ne sont pas redistribués avec cette extension ; les dépendances nécessaires au jeu restent celles de l’installation osu!lazer.
