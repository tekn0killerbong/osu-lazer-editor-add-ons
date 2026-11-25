# Publier EditorAddons v1.0 sur GitHub

- Dépôt : **osu-editor-add-ons**.
- Extension : **EditorAddons**, version interne **1.0.0**.
- Tag de release : **v1.0**.
- DLL publique : **osu.Game.Rulesets.EditorAddons.dll**.

## Préparer les sources

Extraire `osu-editor-add-ons-sources-github.zip` dans le dossier `source` de ton profil Windows. Le ZIP contient déjà le dossier `osu-editor-add-ons`.

Le résultat attendu est `%USERPROFILE%\source\osu-editor-add-ons\README.md`, à côté de `EditorAddons.sln`, `src`, `tests`, `scripts` et `.github`. Conserver les fichiers dont le nom commence par un point.

Ce ZIP contient le dépôt public complet pour une seule extension. Utiliser un dossier propre pour éviter d’y ajouter des sources privées provenant d’un ancien paquet regroupant plusieurs modules. Si le dépôt local est déjà configuré, conserver `.git`, mais remplacer l’ancien contenu par ce paquet complet plutôt que de simplement fusionner les dossiers. Garder une copie des fichiers personnels hors du dépôt public.

Les sources extraites vont dans le dépôt ; le ZIP de release contenant la DLL sera joint séparément à la release.

## Créer un nouveau dépôt

Sur [GitHub — New repository](https://github.com/new), choisir le nom **osu-editor-add-ons** et la visibilité **Public**. Ne pas ajouter de README, de `.gitignore` ni de licence dans le formulaire : ils sont fournis.

Dans PowerShell, avec Git déjà installé et configuré :

```powershell
Set-Location (Join-Path $env:USERPROFILE 'source\osu-editor-add-ons')
git init -b main
git add .
git status --short
git commit -m "Prepare EditorAddons v1.0 for publication"
```

Vérifier la liste des fichiers avant le commit : le projet source doit être `src/EditorAddons`. Remplacer `TON-PSEUDO` par le propriétaire du dépôt, ou copier son URL HTTPS exacte depuis GitHub :

```powershell
git remote add origin https://github.com/TON-PSEUDO/osu-editor-add-ons.git
git push -u origin main
```

Pour un dépôt déjà initialisé, ne pas répéter `git init` ni `git remote add`. Après remplacement des fichiers, utiliser `git add -A`, vérifier `git status --short`, puis faire un nouveau commit et `git push`. Les suppressions des anciens fichiers doivent figurer dans ce commit.

## Vérifier Actions

Ouvrir l’onglet **Actions**, puis **Build and verify**. Attendre que les deux tâches Windows et Linux soient vertes. Elles compilent EditorAddons, exécutent les tests et fournissent un artefact contenant la DLL et les notices.

Les artefacts s’appellent `osu-editor-add-ons-windows-latest` et `osu-editor-add-ons-ubuntu-latest`. Le workflow ne publie pas de release automatiquement.

## Publier la release

Ouvrir **Releases → Draft a new release** :

- Tag : **v1.0**, sur `main`.
- Titre : **EditorAddons v1.0**.
- Description : contenu de `RELEASE-NOTES.md`.
- Pièce jointe : **osu-editor-add-ons-v1.0-release.zip**.
- Facultatif : joindre aussi **osu.Game.Rulesets.EditorAddons.dll**, extraite de ce ZIP.

Relire puis choisir **Publish release**. Les téléchargements **Source code** générés par GitHub ne contiennent pas la DLL compilée.

## Recompiler

Installer le **SDK .NET 10**, puis lancer depuis la racine :

```powershell
.\scripts\build.ps1
```

Le script recrée `dist` avec la seule DLL publique et ses notices. Exécuter les vérifications du README, puis créer le ZIP :

```powershell
Compress-Archive -Path .\dist\* -DestinationPath .\osu-editor-add-ons-v1.0-release.zip -Force
```

Pour une modification ultérieure de la DLL déjà publiée, préparer une nouvelle version et un nouveau tag.

## Références

- [GitHub : ajouter du code local](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github?platform=windows)
- [GitHub : gérer les releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository?tool=webui)
