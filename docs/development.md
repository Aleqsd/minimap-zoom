# Compilation et maintenance

Prérequis : Windows x64, SDK .NET 10, bibliothèques locales de Dalamud API 15. Le projet utilise `Dalamud.NET.Sdk/15.0.0` avec DalamudPackager et son lockfile. Les bibliothèques du jeu et de Dalamud sont fournies par l’installation Dalamud ; aucun de ces binaires ni police n’est distribué avec le plugin.

Pour compiler et préparer le ZIP sans recopier de DLL vers un emplacement d’exécution :

```powershell
dotnet build src/MinimapZoom.csproj -c Release '-p:DalamudLibPath=D:\Dalamud' -p:RestoreLockedMode=true
```

Le SDK accepte aussi `DALAMUD_HOME`. L’argument local historique `-p:DalamudHome=...` reste disponible. La DLL est produite dans `src/bin/Release/net10.0-windows/` ; DalamudPackager génère `MinimapZoom/latest.zip` et son manifeste dans ce même dossier. Le ZIP contient la DLL, les symboles, les JSON nécessaires et la licence à sa racine.

Le script suivant copie aussi les fichiers vers les emplacements de test locaux :

```powershell
.\build.ps1 -DalamudHome 'D:\Dalamud' -Dotnet 'dotnet' -Check -GameExecutable 'D:\FFXIV\game\ffxiv_dx11.exe'
```

Le SDK facultatif `../.tools/dotnet/dotnet.exe` est utilisé s’il existe ; sinon le script emploie `dotnet` du PATH. Chaque dépôt reste compilable seul. `-SourceCommit` permet d’inscrire le commit vérifié dans `build-info.json` pour une release.

Le build produit `releases/<version>/` et `plugin/`. Il vérifie la version assembly/manifeste et les empreintes des copies. **La copie vers `plugin/` peut déclencher un rechargement si Dalamud surveille ce chemin.** Une recompilation remplace les fichiers de la même version ; augmenter les versions du projet et du manifeste pour conserver un nouvel essai.

Les symboles restent dans un PDB portable pour conserver le fonctionnement du script local. Ses chemins source sont neutralisés avec `PathMap` et la génération automatique de SourceLink est désactivée. Le script local refuse un PDB contenant un chemin de profil utilisateur ; le commit de provenance reste inscrit dans `build-info.json` pour les copies produites par ce script.

Les contrôles utilisent les classes de production sur des allocations isolées. Les contrôles du binaire lisent le fichier du jeu sur disque, sans ouvrir son processus. Un client différent est refusé : reprendre [l’analyse native](native-mapping.md) avant de modifier les contrats.

## Essais avec une DLL locale

L’installation courante et les mises à jour passent par le [dépôt personnalisé](../README.md#installation-et-mises-à-jour). Pour tester une compilation locale, conserver `MinimapZoom.json` à côté de la DLL, ajouter son chemin complet dans `/xlsettings` → **Experimental** → **Dev Plugin Locations**, puis scanner les plugins de développement dans `/xlplugins`.

Charger une seule copie de Minimap Zoom à la fois, catalogue ou développement. Pour revenir au catalogue, désactiver la copie de développement et retirer uniquement son entrée avant l’installation normale. Conserver les fichiers de configuration.

Une release dans ce dépôt source ne met pas automatiquement à jour le catalogue. Sa maintenance reçoit la version, le tag et le commit exacts, le ZIP et ses SHA-256 (archive et DLL), ainsi qu’un bref changelog. Les archives d’installation sont vérifiées et publiées avant l’index ; les versions déjà publiées ne sont pas remplacées.

## Interface

`SettingsWindow` utilise le système de fenêtres Dalamud. `SettingsPanel` transmet les changements à des actions ; le plugin les applique sur le thread du jeu avec sauvegarde différée. `SettingsTheme` centralise les couleurs et dimensions fixes du panneau. La personnalisation concerne uniquement la mini-carte native, son cadre et ses marqueurs.

La fenêtre utilise la police active de Dalamud et son échelle globale. Elle ne charge aucune police Windows et ne possède aucun choix de thème, police, couleurs ou disposition. Le déplacement et le redimensionnement habituels restent disponibles.

Le champ obsolète `WindowAppearance` de la version 0.4.0 est ignoré à la lecture, sans réinitialiser zoom, cadre, marqueurs ou démarrage. Il disparaît au prochain enregistrement. Les lignes passent de deux colonnes à une disposition verticale quand la largeur disponible diminue.

## Rendus hors jeu

```powershell
dotnet run --project tools/Preview/Preview.csproj -c Release '-p:DalamudHome=D:\Dalamud' -- 'D:\Dalamud' 'artifacts/ui-preview'
```

Cet outil utilise le même `SettingsPanel` et le même thème, avec le moteur ImGui local de Dalamud. Il rasterise ses vraies listes de dessin et injecte des événements souris pour vérifier les contrôles et le défilement. Les données sont fictives. La police Segoe UI provient de Windows uniquement pour cette exécution ; elle n’est ni copiée ni distribuée dans le dépôt.

Le rendu d’une ancienne configuration avec des valeurs extrêmes de style est comparé pixel par pixel au défaut. Un changement du cadre natif vérifie aussi que les couleurs et dimensions du panneau restent identiques. Les images publiques ne prouvent pas le rendu du HUD ni l’intégration de la fenêtre dans FFXIV. Les fichiers générés et dépendances copiées pour l’outil restent dans les dossiers ignorés.

## Sources et droits

Le code de ce dépôt et ses textures géométriques sont sous licence MIT. Les bibliothèques [Dalamud](https://github.com/goatcorp/Dalamud), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) et leurs dépendances restent fournies séparément par Dalamud. Les noms, données et textures de FFXIV appartiennent à leurs ayants droit ; aucune texture extraite du jeu n’est redistribuée ici. Les sources utilisées pour identifier les fonctions et icônes sont citées dans l’analyse native ; aucun code de Compass n’a été repris.

L’[icône du plugin](https://github.com/Aleqsd/dalamud-plugins/blob/main/icons/MinimapZoom.svg) est un dessin original distribué sous licence MIT par le catalogue personnalisé.

Le plugin a été développé avec une assistance IA. La release GitHub est un essai indépendant, sans soumission au catalogue officiel Dalamud.

La [préparation D17](submission-preparation.md) décrit le périmètre natif, les limites et les points à soumettre à l’avis des approbateurs.
