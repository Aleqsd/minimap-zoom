# Compilation et maintenance

Prérequis : Windows x64, SDK .NET 10, bibliothèques locales de Dalamud API 15. Les dépendances sont fournies par l’installation Dalamud ; aucun binaire de dépendance ni police n’est distribué avec le plugin.

```powershell
.\build.ps1 -DalamudHome 'D:\Dalamud' -Dotnet 'dotnet' -Check -GameExecutable 'D:\FFXIV\game\ffxiv_dx11.exe'
```

Le SDK facultatif `../.tools/dotnet/dotnet.exe` est utilisé s’il existe ; sinon le script emploie `dotnet` du PATH. Chaque dépôt reste compilable seul. `-SourceCommit` permet d’inscrire le commit vérifié dans `build-info.json` pour une release.

Le build produit `releases/<version>/` et `plugin/`. Il vérifie la version assembly/manifeste et les empreintes des copies. **La copie vers `plugin/` peut déclencher un rechargement si Dalamud surveille ce chemin.** Une recompilation remplace les fichiers de la même version ; augmenter les versions du projet et du manifeste pour conserver un nouvel essai.

Les contrôles utilisent les classes de production sur des allocations isolées. Les contrôles du binaire lisent le fichier du jeu sur disque, sans ouvrir son processus. Un client différent est refusé : reprendre [l’analyse native](native-mapping.md) avant de modifier les contrats.

## Interface

`SettingsWindow` utilise le système de fenêtres Dalamud. `SettingsPanel` transmet les changements à des actions ; le plugin les applique sur le thread du jeu avec sauvegarde différée. `SettingsTheme` centralise couleurs et dimensions. Les préférences de fenêtre sont indépendantes du cadre natif et survivent à `/minizoom off`.

Les nouvelles installations utilisent LMeter ; les anciennes gardent les couleurs et la police Dalamud. Expressway et Segoe UI sont recherchées dans les polices Windows et chargées via l’atlas géré de Dalamud. Si une police manque ou échoue, le rendu utilise le repli Dalamud. Les changements de police reconstruisent un handle hors de `Draw`, puis libèrent le précédent. Aucun fichier de police n’est embarqué.

La couleur/opacité du fond est indépendante du texte. Le contour ou l’ombre et les offsets concernent les libellés et l’état de la fenêtre ; ils ne modifient pas les textes natifs du jeu. Les lignes passent de deux colonnes à une disposition verticale quand la largeur disponible diminue.

## Rendus hors jeu

```powershell
dotnet run --project tools/Preview/Preview.csproj -c Release '-p:DalamudHome=D:\Dalamud' -- 'D:\Dalamud' 'artifacts/ui-preview'
```

Cet outil utilise le même `SettingsPanel` et le même thème, avec le moteur ImGui local de Dalamud. Il rasterise ses vraies listes de dessin et injecte des événements souris pour vérifier les contrôles et le défilement. Les données sont fictives. La police Segoe UI provient de Windows uniquement pour cette exécution ; elle n’est ni copiée ni distribuée dans le dépôt.

Les images publiques montrent ces réglages hors jeu. Elles ne prouvent pas le rendu du HUD ni le chargement des polices via les services Dalamud dans FFXIV. Les fichiers générés et dépendances copiées pour l’outil restent dans les dossiers ignorés.

## Sources et droits

Le code de ce dépôt et ses textures géométriques sont sous licence MIT. Les bibliothèques [Dalamud](https://github.com/goatcorp/Dalamud), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) et leurs dépendances restent fournies séparément par Dalamud. Les noms, données et textures de FFXIV appartiennent à leurs ayants droit ; aucune texture extraite du jeu n’est redistribuée ici. Les sources utilisées pour identifier les fonctions et icônes sont citées dans l’analyse native ; aucun code de Compass n’a été repris.

Le plugin a été développé avec une assistance IA. La release GitHub est un essai indépendant, sans soumission au catalogue Dalamud.
