# Préparation D17

Cette branche prépare le build et la documentation pour un avis préalable, sans changer les fonctions natives. Elle conserve la version `0.4.1` / assembly `0.4.1.0` pour cette préparation uniquement : son nouveau binaire ne remplace pas la release ni la version du catalogue personnalisé. Aucune soumission officielle n’est publiée par cette branche.

Le projet suit le [SDK du plugin exemple officiel](https://github.com/goatcorp/SamplePlugin/blob/master/SamplePlugin/SamplePlugin.csproj) et la [préparation de dépôt D17](https://github.com/goatcorp/DalamudPluginsD17#preparing-your-repository) : `Dalamud.NET.Sdk/15.0.0`, DalamudPackager, compilation Release et `src/packages.lock.json` committé. Le chemin de projet du manifeste D17 est `src`.

## Fichiers et effets natifs

| Fichier | Responsabilité |
| --- | --- |
| [ZoomPolicy.cs](../src/ZoomPolicy.cs) | Bornes du zoom, valeurs non finies, paramètre sauvegardé dans la plage native. |
| [NativeZoomOperations.cs](../src/NativeZoomOperations.cs) | Coefficients du fond et des marqueurs, appels natifs de rafraîchissement, bouton de dézoom. |
| [NativeMinimap.cs](../src/NativeMinimap.cs) | Vérification du client, résolution des fonctions et activation des hooks. |
| [NativeMarkerRange.cs](../src/NativeMarkerRange.cs) | Hooks temporaires de `AgentHUD.UpdateNaviMap` et de la collecte des marqueurs. |
| [MarkerRangeScope.cs](../src/MarkerRangeScope.cs) | Rayon de collecte, portée des marqueurs fixes et restauration en sortie. |
| [MinimapAppearance.cs](../src/MinimapAppearance.cs) | Masque carré, collisions, visibilité et échelles des nœuds, restauration. |
| [MinimapFrame.cs](../src/MinimapFrame.cs) | Assets privés du cadre et libération des textures. |
| [SquareMaskPixels.cs](../src/SquareMaskPixels.cs), [FramePixels.cs](../src/FramePixels.cs) | Pixels géométriques du masque et des cadres produits par le plugin. |
| [MarkerPolicy.cs](../src/MarkerPolicy.cs) | Catégories masquées et exceptions de redimensionnement. |
| [NativeContracts.cs](../src/NativeContracts.cs), [NativeLayout.cs](../src/NativeLayout.cs) | Version/empreinte du client, signatures, tailles et offsets attendus. |
| [Plugin.cs](../src/Plugin.cs) | File d’actions sur le thread du jeu, cycle de vie de `_NaviMap`, restauration et gestion des erreurs. |

Les signatures, offsets et calculs issus du client sont détaillés dans la [cartographie native](native-mapping.md).

## Bornes et informations affichées

- Zoom natif : **0,5–2**. Zoom du plugin : **0,25–2**. Le zoom du fond et celui des positions des marqueurs restent synchronisés. La caméra 3D et la grande carte ne sont pas modifiées.
- Sous 0,5, le multiplicateur de portée vaut `0,5 / zoom`, borné entre 1 et 2. À 0,25, le rayon natif des icônes fixes `175 / échelle de carte` devient `350 / échelle de carte` ; son carré est multiplié par quatre. Le rayon de collecte des événements passe de 200 à 400 dans le chemin analysé. Ce sont des unités internes du client, pas des coordonnées de carte affichées.
- L’élargissement utilise le vecteur natif de la mini-carte et les données déjà présentes dans le client. Il peut sélectionner des marqueurs que la distance native aurait écartés. Il n’ajoute pas de requête serveur, de balayage de table d’objets ou de détection externe.
- **L’élargissement n’a pas de liste blanche réservée aux éthérites, marchands et quêtes.** Il porte sur la collecte native dans ce scope et sur le rayon des icônes fixes. Les types effectivement reçus dépendent du jeu ; il ne faut pas promettre que les ennemis ou tous les autres marqueurs sont exclus de son effet.
- La capacité reste de **100 marqueurs réguliers**, plus le repère du joueur. Le fond reste fourni par le jeu. Le plugin n’obtient pas les données que le client ne possède pas et ne garantit pas que tous les points éloignés seront visibles.
- Les filtres de visibilité sont une fonction distincte : le masquage global préserve le joueur ; les filtres sélectifs préservent notamment les quêtes en cours, l’épopée, les quêtes bleues et les icônes inconnues. Les tailles des icônes et du joueur sont bornées à **50–200 %**, avec exceptions pour les surfaces liées au terrain.

## Protections et PvP

L’activation exige le client `2026.08.11.0000.0000`, son SHA-256 exact, les structures attendues et les signatures natives. Les entrées non finies sont normalisées. L’addon doit être prêt ; les pointeurs conservés servent à identifier l’instance et les ressources sont restaurées avant sa fermeture. Les hooks sont désactivés au déchargement.

La portée élargie exige le drapeau mini-carte et son vecteur exact. Les rayons d’origine sont restaurés dans `finally`, sans écraser une nouvelle valeur produite par le jeu. Le zoom sauvegardé revient dans les bornes natives en préservant les bits du verrouillage du nord. Les marqueurs sont restaurés avant les mises à jour natives, puis les préférences sont réappliquées après et avant le dessin. Masques et cadres utilisent des ressources privées, détachées avant libération. Une erreur déclenche la restauration et un diagnostic.

**Aucune désactivation PvP n’est implémentée actuellement.** Le plugin agit lorsque `_NaviMap` est disponible et compatible ; il ne teste ni `IsPvP` ni un type de contenu compétitif. Son comportement effectif en PvP n’a pas été validé pendant cette préparation. Le fait de réutiliser des données natives ne prouve pas l’absence d’avantage.

Les [restrictions officielles](https://dalamud.dev/plugin-publishing/restrictions/) rendent l’impact sur les informations visibles, le combat et le PvP pertinent pour l’avis préalable. La limite de zoom caméra citée dans ces règles ne constitue pas, à elle seule, une décision sur le zoom de la mini-carte. L’admissibilité du dézoom étendu et de la portée accrue reste à discuter avec les approbateurs ; aucun accord n’est présumé et aucun message ne leur a été envoyé dans cette préparation.

## Provenance et validation

Le niveau déclaré pour les passes autonomes de code est **Auto — OpenAI Codex**, avec direction produit, idées et retours humains. L’auteur confirme avoir testé les plugins en jeu ; cela ne signifie pas qu’il a relu chaque ligne ni testé le nouveau SHA de cette branche. Le paragraphe du README décrit cette implication sans réduire la contribution de Codex.

L’icône, le masque carré et les textures de cadres ont été créés avec l’aide de Codex ; la Description du manifeste le signale. Les textures géométriques sont générées par le code du plugin. La capture en jeu fournie par l’auteur reste une capture, pas une image générée. La [politique IA](https://dalamud.dev/plugin-publishing/ai-policy/) a été relue pour cette préparation.

Les vérifications locales portent sur le build Release isolé, la restauration verrouillée des dépendances, le ZIP et son manifeste, l’absence de chemins privés et les contrôles natifs hors jeu. Elles ne prouvent ni un build du service D17 ni le chargement du nouveau binaire dans FFXIV. L’avis préalable et l’essai du SHA proposé restent distincts de cette préparation technique.
