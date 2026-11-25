# Changements

## EditorAddons 1.0.0 — publication v1.0

- EditorTools devient EditorAddons : projet, assembly, namespace, carrier, ressource Harmony et DLL renommés.
- Dépôt public `osu-editor-add-ons`, limité aux outils d’édition et aux contrôles de lecture.
- Fonctionnalités d’édition conservées ; identifiants de raccourcis et fichiers de préférences maintenus pour les installations précédentes.
- Sources, tests, compilation automatique Windows/Linux et documentation adaptés à la DLL unique.
- À l’installation, remplacer l’ancienne DLL EditorTools par `osu.Game.Rulesets.EditorAddons.dll`.

## Historique des outils d’édition

## 0.3.0

- Mania et Taiko : animation native entre les steps, avec conservation de la destination logique pendant les saisies rapides.
- Mania et Taiko : grille native visible lorsque le recorder est activé, y compris en Select sans sélection.
- Taiko : un hit existant est réutilisé ; les doublons au même timestamp sont regroupés. Les arrondis d’import sont pris en compte et l’avancement reste d’une division complète.
- Mania : les notes sur le corps ou la fin d’une LN la raccourcissent jusqu’à la ligne précédente. Même espace lors d’un allongement vers une note existante. Les maintiens réduits à zéro deviennent des notes courtes.
- Catch : suivi automatique de Stick to curve après modification d’objets précédents, avec guides locaux persistants, annulation/rétablissement et commande Restore original curve.
- Catch : retrait de Convert selected vertices to fruits, qui ne supprimait pas les gouttes générées. La conversion native du stream complet reste disponible.
- Catch : Track current droplet positions adopte les positions actuellement correctes d’une ancienne conversion ou d’un export réimporté. Il ne peut pas retrouver une ancienne trajectoire déjà perdue ; les guides restent locaux.
- Mania/taiko : inversion du sens de Decrease/Increase grid spacing ; 7 diminue la subdivision, 8 l’augmente, dans cet ordre dans les bindings. Catch conserve le comportement précédent.
- Mania/taiko/catch : Ctrl/Shift passent à Select avec clic ou rectangle ; Ctrl conserve les groupes disjoints, Shift étend une plage depuis le dernier groupe. Le clic droit de suppression natif reste intact.
- Tous les éditeurs : transfert des sliders Playback speed 1–200 %, Pitched et application en mode Test dans la DLL optionnelle. Migration de la préférence Pitched ; pause/reprise/retry et nettoyage au retour.

## 0.2.0

- Compensation persistante de Stick to curve dans la géométrie native, jouable sans l’extension et conservée à l’export.
- Tolérance d’un pixel osu!, conservation des types et timings, refus des répétitions.
- Cette version ne conservait pas de guide pour recalculer la compensation après des modifications antérieures.

## 0.1.0

- Première extension indépendante pour les éditeurs Mania, Taiko et Catch.
- Step recorder, bindings de gameplay, Return on pause et tracé manuel des juice streams.
- Stick to curve limité à un aperçu temporaire ; remplacé en 0.2.0.
