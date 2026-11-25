# EditorAddons — guide d’utilisation

Extension indépendante pour les éditeurs **osu!mania, osu!taiko et osu!catch**, avec contrôles de lecture communs aux éditeurs d’osu!lazer. Version **1.0.0**, publication **v1.0**, dépôt **osu-editor-add-ons**. Auteur : tekno_killerbong.

## Installation et remplacement d’EditorTools

1. Dans lazer, utiliser **Open osu! folder**, puis fermer complètement le jeu.
2. Ouvrir le sous-dossier `rulesets` et y placer `osu.Game.Rulesets.EditorAddons.dll`.
3. Pour une mise à jour depuis EditorTools, retirer `osu.Game.Rulesets.EditorTools.dll` de `rulesets`. Déplacer les sauvegardes hors de ce dossier pour éviter le chargement simultané des deux versions.
4. Relancer lazer.

La cible de compilation est **osu!lazer 2026.916.0 / .NET 10**. La compatibilité avec d’autres versions du client doit être vérifiée. Le carrier technique reste masqué : aucun mode de jeu supplémentaire n’apparaît. En cas d’échec de chargement, consulter les journaux et retirer la DLL pour revenir aux éditeurs natifs.

Les noms historiques des fichiers de préférences `editor-tools*` sont conservés afin de retrouver les réglages et les guides de courbes déjà enregistrés. Les raccourcis personnalisés gardent leurs identifiants.

## osu!mania

En haut de la colonne de gauche : **Step recorder**, puis **Return on pause**, avec le même agencement et le symbole des deux pas de Shred.

- Activer le recorder et utiliser les touches de **Gameplay → osu!mania**, pour le nombre de colonnes de la difficulté. Les changements de bindings sont lus en direct.
- Un appui crée une note. Le curseur avance d’une division après le relâchement de la dernière touche de l’accord, avec l’animation fluide native. Les entrées rapides utilisent immédiatement la destination du mouvement, sans attendre sa fin.
- Maintenir une touche et appuyer sur **Empty step** pour transformer la note en LN, puis l’allonger. Le relâchement d’une LN n’ajoute pas de pas supplémentaire.
- Une nouvelle note courte peut être ajoutée pendant le maintien d’une LN. Les flèches gauche/droite naviguent sur la grille et ajustent les notes maintenues.
- Le mode **Select** accepte ces entrées. La sélection des hitsounds et des banques reste celle de l’éditeur natif. Les créations participent à son historique d’annulation.
- Une note placée sur le corps ou exactement à la fin d’une LN raccourcit celle-ci jusqu’à la ligne de grille précédente, dans la même colonne. Il reste donc une division entière avant la nouvelle note. Si la durée devient nulle, la LN devient une note courte. Une note à la tête d’une LN remplace celle-ci.
- L’allongement d’une LN laisse aussi une division avant la prochaine note. Le calcul suit la grille actuelle, y compris les triolets et les changements de BPM. Les têtes à moins d’une milliseconde sont traitées comme le même emplacement pour gérer les arrondis des cartes importées.
- La grille native reste visible dans tout le terrain lorsque le recorder est activé, même en **Select**, sans sélection et hors survol de la souris. Son espacement suit le réglage courant. La visibilité habituelle revient lorsque le recorder est désactivé.

## osu!taiko

Même panneau et mêmes commandes. Les touches proviennent de **Gameplay → osu!taiko**.

- Une touche de centre : Don ; une touche de bord : Katsu.
- Les deux touches de centre dans un même geste maintenu : Big Don ; les deux touches de bord : Big Katsu.
- Avancement fluide après le relâchement complet du geste ; la grille reste visible tant que le recorder est activé, comme dans mania.
- Enregistrer sur un hit existant met à jour sa couleur et sa force au lieu d’en superposer un autre. Les doublons déjà présents à cet emplacement sont regroupés ; une tolérance d’une milliseconde gère les timestamps arrondis des imports.
- Si les couleurs sont mélangées dans un geste, la dernière couleur appuyée détermine la note.
- Drumrolls et swells restent créés avec les outils à la souris.

## Lecture et Return on pause

Ce réglage est indépendant de l’activation du recorder, dans mania et taiko.

- `Espace` : lecture/pause. Si **Return on pause** est coché, la pause ramène au point de départ de la lecture.
- `Ctrl+Espace` : lecture/pause ; à la pause, rester à la position courante.
- Une navigation pendant la lecture met à jour le point de retour.
- Si Espace est une touche de gameplay et que le recorder est actif, elle sert à enregistrer ; utiliser Ctrl+Espace pour la lecture.

## osu!catch

- Outil **Juice stream** : cliquer dans le terrain, maintenir le bouton gauche et dessiner vers les temps suivants. Chaque division temporelle traversée reçoit un sommet interpolé, même lors d’un déplacement rapide. Relâcher pour terminer au temps aimanté. Le dessin clic par clic reste disponible.
- La tête et la fin suivent la représentation native des juice streams. La fin n’est pas dupliquée ; la conversion vers les courbes natives peut ajouter des points de contrôle techniques.
- **Convert selected vertices to fruits** a été retiré : cette ancienne commande ajoutait des fruits et supprimait des sommets, mais le moteur régénérait les gouttes. Elle ne réalisait donc pas une conversion fidèle. Un sommet n’est pas une goutte indépendante ; changer seulement le type de certaines gouttes n’est pas représentable dans un stream natif arbitraire. La commande native **Convert to stream** reste disponible pour convertir le stream entier.
- **Stick to curve (automatic)** convertit la géométrie du stream pour que les petites gouttes rejoignent la trajectoire d’origine en gameplay. Les grandes gouttes et les fruits conservent aussi leurs positions visées. L’opération conserve le type, le nombre et le timing des objets ; elle ne transforme pas les gouttes en fruits.

**Conversion persistante :** le format `.osu` ne stocke pas directement une position indépendante pour chaque petite goutte. La conversion ajoute donc des points de contrôle à la courbe native pour compenser le décalage que le jeu applique ensuite. Elle peut augmenter la vitesse de slider. La nouvelle courbe technique peut zigzaguer : la trajectoire visée est celle d’avant la conversion, pas cette nouvelle courbe. La sauvegarde, la réouverture et l’export utilisent uniquement les données natives ; les joueurs n’ont pas besoin de cette DLL. Aucun traitement des positions en gameplay n’est modifié par l’extension.

La conversion vérifie une erreur maximale de **1 pixel osu!** pour chaque position, car les coordonnées de l’export classique sont entières. Sur les six scénarios d’essai, l’écart maximal mesuré est de **0,576 pixel**, sans changement de type, de nombre ou de timing. Ce n’est pas une garantie d’égalité mathématique parfaite sur toute carte.

**Suivi automatique depuis 0.3.0 :** la courbe d’origine est conservée localement. L’éditeur recalcule la compensation après l’ajout, la modification ou la suppression de streams et banana showers précédents. Ce suivi fonctionne après sauvegarde/réouverture et avec l’annulation/rétablissement natifs. Un nouveau clic sur un stream déjà suivi n’applique pas une seconde compensation.

Pour modifier la forme du stream aligné, utiliser **Restore original curve**, éditer ce tracé, puis appliquer **Stick to curve** à nouveau. Une modification directe des points techniques change la signature du stream et n’est plus suivie automatiquement.

Les trajectoires originales sont enregistrées dans `editor-tools-curves-<identifiant de difficulté>.json`, dans le dossier de données de lazer. Elles restent locales à cette installation et cette difficulté : elles ne sont pas incluses dans l’export `.osu`/`.osz` ni transférées automatiquement à une difficulté copiée. Les exports restent jouables sans l’extension grâce à leur géométrie compensée. Pour continuer l’édition automatique, garder les fichiers de suivi avec les données lazer. Une édition dans un autre client sans ces guides peut encore invalider la compensation. Les mods qui modifient les positions ou la séquence aléatoire ne garantissent pas l’alignement avec la trajectoire d’origine.

**Migration depuis 0.2.0 :** avant de modifier les objets précédents, sélectionner le stream puis **Track current droplet positions**. Cette commande conserve les positions de gameplay actuellement correctes comme guide, au lieu de suivre la courbe technique en zigzag. Si les positions ont déjà été dérangées, revenir d’abord à une version où elles sont correctes : la 0.2.0 n’avait pas sauvegardé la trajectoire originale et celle-ci ne peut pas être devinée. La même commande permet de reprendre le suivi après un export/réimport ou sur un autre ordinateur.

Les streams à répétitions sont refusés, car chaque passage utilise la même courbe mais des décalages différents. Les anciennes dispositions de ticks incompatibles, les compensations dépassant la vitesse native autorisée et les conversions ne respectant pas les contrôles de fidélité sont également refusées. Une première conversion refusée laisse le stream intact. Si une modification ultérieure rend un recalcul impossible, le tracé original est restauré et une notification l’indique, afin de ne pas conserver silencieusement un alignement invalide.

## Sélection dans mania, taiko et catch

- **Ctrl ou Shift + clic gauche** dans le terrain passe à Select, même si un outil de création est actif. Une création en cours est annulée.
- **Ctrl + clic** ajoute un objet, ou le retire s’il est déjà sélectionné. **Ctrl + glisser** ajoute un rectangle à la sélection sans déplacer les notes déjà sélectionnées.
- **Shift + clic/glisser** étend la sélection en plage temporelle entre le dernier objet/groupe choisi et la nouvelle cible. Les autres groupes déjà sélectionnés restent sélectionnés. La plage inclut les notes intermédiaires de toutes les colonnes.
- Les gestes sans Ctrl/Shift restent natifs. Dans Catch, les gestes sur les sommets d’un stream déjà sélectionné conservent leur édition native.
- **Clic droit avec un outil de création actif** supprime déjà les objets dans les éditeurs natifs ; ce comportement est conservé. Dans Select, le menu contextuel reste accessible.

## Playback speed et mode Test

Les contrôles précédemment intégrés à Shred sont maintenant fournis par la DLL optionnelle. Ils s’installent dans la barre commune à tous les éditeurs, y compris standard et l’éditeur Shred lorsqu’il est installé ; aucun code de gameplay Shred n’est incorporé ou modifié.

- Slider continu **1 % à 200 %**, précision de **1 %**, pourcentage affiché à droite. Les flèches ajustent le slider lorsqu’il a le focus.
- **Playback speed | Pitched** : activé, la hauteur du son suit la vitesse ; désactivé, le tempo change en conservant la hauteur à partir de 5 %. Sous 5 %, une réduction supplémentaire de hauteur est nécessaire pour respecter les limites du moteur audio.
- La préférence **Pitched** est mémorisée dans `editor-tools-playback.ini`. Au premier lancement, la valeur de l’ancien `shred-editor-playback.ini` est reprise s’il existe. Sinon, Pitched est activé par défaut.
- La vitesse reste propre à la session d’éditeur : 100 % à l’ouverture, conservée après un test et lors du passage par Timing. Dans Timing, le slider est désactivé sans perdre la vitesse choisie.
- **Test** reprend la vitesse et le mode Pitched sélectionnés. La pause, la reprise et le redémarrage du test conservent ces réglages. À la sortie, les facteurs du test sont retirés pour éviter leur multiplication au retour dans l’éditeur.
- Le gameplay lancé normalement n’hérite pas de ces réglages.

Cette migration vise Shred débarrassé de ses anciens contrôles communs. La coexistence avec une ancienne DLL Shred qui installe encore son propre service de vitesse n’a pas été validée ; utiliser la version Shred correspondante quand les deux DLL sont installées.

## Bindings

Les nouvelles lignes sont dans **Settings → Bindings → Editor → osu!mania / osu!taiko / osu!catch**. Les touches de création des notes restent configurées dans les sections **Gameplay** correspondantes.

| Action | Mania et Taiko | Catch |
| --- | --- | --- |
| Toggle step recorder | Ctrl+Q | — |
| Empty step | é, touche physique Slash sur clavier canadien-français | — |
| Decrease grid spacing | 7 : 1/4 → 1/2 | S : 1/4 → 1/8 |
| Increase grid spacing | 8 : 1/4 → 1/8 | A : 1/4 → 1/2 |
| Decrease grid type | 9 | D |
| Increase grid type | 0 | F |

Dans mania et taiko, le sens des deux actions de spacing est inversé par rapport à la 0.2.0, selon la convention demandée. Les lignes restent dans l’ordre Decrease, Increase. Catch garde son comportement précédent. Les touches personnalisées ne sont pas réinitialisées.

Chaque nouvelle action a deux emplacements ; le deuxième est vide par défaut. Sur un autre clavier, réassigner Empty step à la touche voulue. Les chiffres sont ceux de la rangée supérieure.

Le type de grille parcourt Common, Triplets et la grille personnalisée si elle a été utilisée ; Common démarre à 1/4 et Triplets à 1/6. Les touches restent ajustables dans les réglages.

Les raccourcis natifs Q/W/E/R et leurs variantes Shift et Alt+Shift sont rétablis une fois lorsqu’une action a tous ses emplacements vides. Un raccourci personnalisé non vide est conservé. Q correspond au nouveau combo ; W/E/R aux hitsounds. Dans Catch, T conserve son rôle natif de distance snap. Si une touche de gameplay entre en conflit, le recorder activé lui donne priorité ; le désactiver rend le raccourci natif disponible.

Les préférences des deux interrupteurs sont enregistrées dans `editor-tools.json`. La migration des hitsounds est marquée par `editor-tools-sound-bindings-v1.txt`. Les bindings eux-mêmes utilisent la base de réglages native de lazer.

## Compilation et dépôt GitHub

Installer le SDK .NET 10. Depuis ce dossier :

```sh
dotnet build src/EditorAddons/EditorAddons.csproj -c Release
dotnet run --project tests/EditorAddons.Tests -c Release
python3 scripts/verify-discovery.py
dotnet run --project tests/EditorAddons.Tests -c Release -- --curve
dotnet run --project tests/EditorAddons.Tests -c Release -- --editor
dotnet run --project tests/EditorAddons.Tests -c Release -- --playback
```

Le binaire est produit dans `src/EditorAddons/bin/Release/net10.0`. **Distribuer uniquement `osu.Game.Rulesets.EditorAddons.dll` avec les notices**, pas les DLL du jeu présentes dans les sorties de compilation. Harmony est embarqué dans la DLL.

Les scripts `scripts/build.sh` et `scripts/build.ps1` produisent le dossier `dist`. Le dossier source est prêt à être la racine d’un dépôt GitHub : projets, tests, `.gitignore`, workflow de compilation, documentation et licences. Aucun dépôt distant n’a été créé ni publié.

## Licence

Les contributions originales utilisent la licence **Editor Tools — usage non commercial 1.0** (`LICENSE.md`) : utilisation, modification et redistribution gratuites ; vente et exploitation commerciale interdites. C’est une licence spécifique préparée selon la consigne de l’auteur, pas une licence MIT ni une licence open source permissive. Les composants tiers conservent leurs propres licences dans `LICENSES` et `THIRD_PARTY_NOTICES.md`.

Voir `docs/VALIDATION.md` pour les vérifications réalisées et leurs limites.
