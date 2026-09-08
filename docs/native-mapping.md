# Fonctions natives du prototype

**Contrat actuel, depuis 0.5.1 : client `2026.09.01.0000.0000`.** Les nouvelles adresses et les vérifications sont dans [la mise à jour de compatibilité](compatibility-20260901.md). Les sections ci-dessous conservent l’analyse historique du client d’août ; leurs adresses ne sont pas celles du client actuel.

Analyse statique du client `2026.08.11.0000.0000` le 6 septembre 2026. SHA-256 de `ffxiv_dx11.exe` :

```text
74F0408AD357BA35B20B6FAD8C5BFA70C4B07A5A345F8840B3EA368AB395BDF0
```

Les sources de symboles sont figées au commit FFXIVClientStructs `d8633414de71407f9eb45da830472e6e0fe26a08` :

- [ida/data.yml](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/ida/data.yml), version identique au client.
- [AddonNaviMap](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Client/UI/AddonNaviMap.cs).
- [Atk2DMap](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Client/UI/Atk2DMap.cs).

Les RVA suivants servent uniquement à vérifier les résultats du scan hors jeu. Le plugin résout les fonctions par leurs signatures dans `NativeContracts.cs`.

| RVA | Nom descriptif | Convention Windows x64 |
| --- | --- | --- |
| `0x157BBF0` | Application/bornage du zoom | `void(AddonNaviMap*, byte refresh)` |
| `0x157BEF0` | Visibilité et flèches de bord d'un marqueur | `void(AddonNaviMap*, uint index, byte force)` |
| `0x157C320` | Transformations des marqueurs et fond | `void(AddonNaviMap*)` |

Le traitement original borne le coefficient entre 0,5 et 2, synchronise les champs `AddonNaviMap + 0x3A78` et `+0x264`, puis, lorsque `refresh == 1`, recalcule les 100 marqueurs réguliers et le fond. Il désactive le bouton de dézoom à 0,5.

Le prototype appelle cette routine originale puis, uniquement sous 0,5, réapplique le coefficient étendu aux deux champs et réutilise les deux routines de rendu. Il réactive aussi le bouton de dézoom à 0,5 et ne le désactive qu'à 0,25. Les valeurs du zoom natif continuent d'utiliser le rendu original.

La routine de fond appelle `Atk2DNaviMap` à la RVA `0x12397B0`, puis `AtkResNode.SetScale` à la RVA `0x660C40` avec le coefficient de zoom sur les deux axes. La transformation des marqueurs utilise aussi `MarkerRadiusScale`. Certains calculs au bord du cercle divisent par le zoom : les valeurs non finies, nulles ou négatives doivent être normalisées avant tout appel natif.

Le second paramètre est représenté par `byte` afin de reproduire explicitement son ABI d'un octet. Le prototype respecte les appels d'initialisation sans rafraîchissement : il ne parcourt pas les marqueurs lorsque `refresh == 0`.

Les essais automatisés utilisent le même `NativeZoomOperations` que le plugin, avec des delegates contrôlés et un `AddonNaviMap` alloué hors du jeu. Ils vérifient l'ordre des opérations et les écritures, sans simuler la totalité du moteur de rendu. Le rendu réel, la sélection des marqueurs et les interactions avec d'autres plugins restent des validations en jeu.

## Apparence en 0.2.0

Le fichier `ui/uld/navimap.uld` du client a été lu avec Lumina. Le masque est le nœud image 17 (`AddonNaviMap.Mask`, offset `0x15F0`), situé en `(21,18)`, de taille `176×176`. Sa part utilise toute la texture `NaviMap_Mask.tex`. La zone de clic est le nœud 19 (`MainCollision`, `0x1590`), situé en `(32,28)`, de taille `156×156`. Les bordures circulaires sont les nœuds image 13 et 15.

Le masque natif utilise du blanc au centre et du noir aux coins, avec un alpha de 255. Le plugin produit un masque avec les mêmes conventions : rectangle blanc correspondant à la zone de clic, marge noire. Il clone uniquement la liste de parts et l'asset de ce nœud et conserve ses coordonnées UV. Les textures et assets partagés du jeu restent inchangés.

La RVA `0x640490`, `AtkTexture.GetTextureWidth`, divise la largeur physique d'une texture de ressource par son facteur HD, mais ne divise pas celle d'une texture de type KernelTexture. La texture créée par le plugin utilise donc les dimensions logiques (176×176), même si la ressource native chargée est `NaviMap_Mask_hr1.tex` (352×352).

`AtkResNode.DrawFlags` bit 23 sélectionne les collisions elliptiques ; il est retiré pour le carré puis restauré. Le bit 18 (`IsDrawDisabled`) masque les deux bordures et, sur demande, les groupes de marqueurs. Les valeurs initiales de ces seuls bits sont conservées, sans réécrire les autres propriétés des nœuds.

Les marqueurs normaux sont les entrées 0–99 de `NaviMap.NaviMapMarkers`. L'entrée 100 correspond au joueur et reste affichée. Les groupes distincts de flèches en bordure se trouvent à `AddonNaviMap + 0x25C0 + index * 0x30`, comme le montre la routine de visibilité à la RVA `0x157BEF0`. Ils sont masqués avec les marqueurs.

Les copies privées sont détachées avant fermeture de l'addon et déchargement du plugin. La texture est rendue au mécanisme natif de libération différée. Les tests d'apparence exécutent le même code de modification des nœuds avec un allocateur de texture simulé ; la création GPU et le rendu réel restent à valider en jeu.

## Marqueurs et cadres en 0.3.0

`NaviMapMarker.IconId` et `SecondaryIconId` sont aux offsets `+0x04` et `+0x08`. Les identifiants de catégories ont été recoupés avec le catalogue [Compass](https://github.com/fitzchivalrik/compass/blob/75a78aecdc39fab2c79c2855894d6e9a5225fadb/Compass/Data/Constant.cs), les entrées `MapSymbol` du client et une planche des textures de quête extraite localement avec Lumina. Il s'agit d'un catalogue conservateur, pas d'un inventaire de toutes les icônes. Les symboles de progression et de validation des quêtes sont volontairement exclus des filtres de disponibilité. Un marqueur avec une seconde icône inconnue ou non filtrée est conservé.

Les échelles sont appliquées aux nœuds des icônes et aux groupes de bordure, avec multiplicateur séparé pour `Atk2DMap.PlayerPin` et le slot 100. Chaque nœud n'est traité qu'une fois, même si ces deux pointeurs désignent le même repère. Les modifications sont restaurées à `PreUpdate`, puis réappliquées à `PostUpdate`. La restauration n'écrase pas une échelle déjà remplacée par le jeu. Aucune coordonnée de marqueur ni échelle du fond n'est modifiée par cette option.

La transformation native à la RVA `0x12397B0` distingue les symboles de taille fixe des surfaces liées au terrain : type bas de `SubtextOrientation` égal à 12, ou `Unknown1C` non nul sans le bit `0x1000`. Ces surfaces, ainsi que les textures de cercle connues, sont exclues du redimensionnement. Les écritures de `ScaleX/ScaleY` reproduisent le réglage des bits dirty/transform de `AtkResNode.SetScale` à la RVA `0x6621A0`, y compris si le nœud reste tourné après restauration.

Le cadre carré remplace la texture du nœud image 15, enfant direct du nœud racine, en `(21,21)` et de taille `176×176`. Le nœud 13 appartient au groupe des points cardinaux tournant ; il est masqué pour le carré. Le cadre privé utilise une texture BGRA transparente, avec ses traits à l'intérieur du rectangle de collision `(32,28)–(188,184)`. Les positions et l'arbre des nœuds restent natifs, donc le cadre suit le déplacement et l'échelle HUD. La texture n'est recréée que si le style, la couleur ou la géométrie change. Le retour au rond ou au mode sans cadre détache les parts privées avant libération.

Les préférences ajoutées en version de configuration 3 sont optionnelles pour les anciens fichiers. La réactivation automatique du zoom est désactivée par défaut ; la fenêtre de réglages reste fermée par défaut, sauf erreur de compatibilité ou d'exécution.

## Portée et masquage corrigés en 0.3.1

Le signalement en jeu sur les icônes fixes a conduit à examiner `AgentHUD.UpdateNaviMap`, RVA `0xFE0460`. Les symboles du [même client](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/ida/data.yml) identifient cette fonction ; les offsets de son vecteur sont recoupés avec [AgentHUD](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentHUD.cs). Les calculs suivants proviennent de l'analyse statique du binaire dont l'empreinte figure en tête de ce document.

- RVA `0xFE09F8–0xFE0A45` : calcul du rayon `175 / AgentMap.MiniMapScale` et de son carré, stockés dans `AgentHUD + 0x4E0C/0x4E10`.
- RVA `0xFE0A70` : collecte des événements, avec un rayon de 200, immédiatement après ce calcul et avant la sélection des icônes.
- RVA `0xFE0BC8–0xFE0C09` : comparaison de la distance X/Z au carré des icônes fixes avec le rayon au carré. Les icônes éloignées sont ignorées, même si le fond a été dézoomé.
- RVA `0xFE0BA0` : limite de 100 marqueurs, conservée intégralement par le plugin.

Deux interceptions supplémentaires ont des signatures vérifiées uniques : `void UpdateNaviMap(AgentHUD*, uint flags)` à `0xFE0460`, et `void CollectMapMarkers(AgentMap*, vector*, byte isMinimap, position*, float radius, byte flag6, byte flag7)` à `0xFBCBF0`. Cette seconde routine compare effectivement la distance à `(radius + taille du repère)^2` à la RVA `0xFBD1D2–0xFBD21B`.

Une portée temporaire existe seulement pendant l'appel à `UpdateNaviMap`. La collecte doit recevoir le vecteur exact `HUD + 0x4B30` et le drapeau mini-carte. Le multiplicateur vaut `0,5 / zoom`, borné entre 1 et 2 : à 0,25, les rayons sont doublés et le carré est multiplié par quatre. Les valeurs d'origine sont capturées après le calcul natif et restaurées dans `finally` à la sortie. Les appels de la grande carte, les autres vecteurs et le zoom natif restent inchangés. Aucune constante globale, capacité de tableau ou donnée serveur n'est modifiée. Le compteur de mises à jour dans Diagnostic permet de vérifier que ce chemin s'exécute réellement en jeu.

Le masquage de la version précédente ne touchait que le bit `IsDrawDisabled` des conteneurs. Le correctif traite aussi leurs éléments présents dans la liste de rendu : cinq pointeurs par marqueur à `AddonNaviMap + 0x15F8 + index*0x28` et cinq enfants de bordure à `+0x25C8 + index*0x30`. Les images principale/secondaire, indicateurs, textes et surfaces sont identifiés dans les routines de rafraîchissement et d'affectation (`0x157BEF0`, `0x157BCE0`). Le bit `NodeFlags.Visible` (`0x10`) est retiré en plus de `DrawFlags` bit 18. Le slot joueur 100 reste exclu.

Les drapeaux d'origine sont restaurés avant `PreUpdate` et `PreRequestedUpdate`, puis les réglages sont réappliqués à `PostUpdate`, `PostRequestedUpdate` et `PreDraw`. Cela permet au jeu de réutiliser les slots et de calculer sa visibilité avant le masquage final. Les drapeaux sans rapport sont préservés. Les contrôles hors jeu couvrent les enfants effectivement dessinés, les mises à jour tardives, les slots réutilisés, le calcul de portée et sa restauration ; le résultat visuel nécessite encore un essai en jeu.

## Décoration soleil/lune en 0.3.2

Le champ `AddonNaviMap.Sun` à `+0x15A8` désigne le nœud image 16, affecté par l'initialisation à la RVA `0x157A3AD`. L'heure alimente sa rotation à `0x157AF3C`. Le réglage `HideSunMoon`, désactivé par défaut, masque ce nœud avec les mêmes garanties de restauration avant mise à jour et de réapplication avant dessin. La rotation et les textures natives continuent d'être gérées par le jeu. Le cadre, les points cardinaux, la météo et le marqueur du joueur restent indépendants.

## Météo et boutons en 0.4.1

La routine d’initialisation `_NaviMap` à la RVA `0x157A250` associe les nœuds 2 et 3 aux boutons de zoom avant/arrière (`+0x15D0/+0x15C8`) et le nœud 4 au verrouillage du nord (`+0x15C0`). Le nœud composant 14 contient la météo : son image enfant 3 et sa collision enfant 5 sont affectées aux champs `+0x15B0/+0x15B8`, aux RVA `0x157A40D/0x157A426`. Il est distinct du soleil/de la lune (nœud 16).

Le plugin masque les nœuds propriétaires, les enfants de chaque composant, leur racine et leur collision, y compris les composants imbriqués. Il utilise les listes natives `AtkUldManager` et vérifie les offsets ainsi que la capacité avant de les parcourir. Les structures sont recoupées avec [AtkComponentBase](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Component/GUI/AtkComponentBase.cs), [AtkComponentNode](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Component/GUI/AtkComponentNode.cs) et [AtkUldManager](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Component/GUI/AtkUldManager.cs), au même commit figé.

Les deux options utilisent le même cycle de restauration/réapplication que soleil/lune : avant les mises à jour natives, après ces mises à jour et avant le dessin. Seuls les bits de visibilité sont possédés ; l’état activé/coché des composants, le paramètre de verrouillage du nord et le coefficient de zoom ne sont pas modifiés. Les essais sur allocations isolées couvrent les combinaisons indépendantes, les collisions, les mises à jour tardives et la restauration ; les infobulles, clics et le rendu réel restent à valider en jeu.

## Décorations, transparence et densité en 0.5.0

Le même `navimap.uld` identifie les lettres N/W/S/E comme quatre images 9–12, enfants du groupe tournant 8. Le masquage cible ces images, sans masquer le groupe 8 qui contient aussi le cadre circulaire 13. Les coordonnées utilisent le groupe 5, son texte 6 et son fond 7 ; les trois nœuds sont traités pour couvrir la liste de rendu aplatie. L’initialisation affecte `Coords` à `+0x1598` et `CoordsText` à `+0x15A0` aux RVA `0x157A3C1/0x157A3D5`. La visibilité initiale reste restaurée selon les mêmes règles que les autres décorations.

`MapImage`, affecté à `+0x15E0` à la RVA `0x157A830`, désigne l’image du terrain dans le composant de carte. La routine `AtkResNode.SetAlpha` à la RVA `0x661550` écrit uniquement l’octet `Color.A` à `+0x83` (corps : test du pointeur, écriture, retour). Le plugin multiplie cet alpha par l’opacité choisie, sans toucher aux canaux RGB, à la racine, au masque ou aux icônes. Il restaure avant mise à jour, réapplique avant dessin et laisse prioritaire un alpha déjà modifié par le jeu. L’offset `Color` et celui de `ByteColor.A` sont vérifiés. Cette analyse et les essais sur allocations ne remplacent pas un essai visuel en jeu.

Les paramètres d’épaisseur (1–8 pixels logiques) et de longueur des angles (8–64) entrent dans la clé du cache de cadre. Une valeur d’épaisseur mémorisée à zéro conserve l’épaisseur historique du style, pour les anciennes configurations. Chaque nouveau cadre détache la texture précédente avant libération ; aucune ressource native partagée n’est modifiée.

La réduction d’encombrement intervient seulement sous 0,50. `MarkerCrowding` compare les positions natives X/Y des marqueurs visibles partageant le même parent. Elle réduit progressivement, jusqu’à 65 % de la taille choisie et sans passer sous 50 % de la taille native, les icônes reconnues de marchands, services, quêtes secondaires disponibles, mandats et Triple Triade. Les distances sont calculées sur les positions non modifiées, sans tenir compte des icônes filtrées. Les surfaces, icônes inconnues ou mixtes protégées, objectifs en cours, groupe, ennemis, éthérites et joueur ne sont pas redimensionnés par cet effet. Les flèches associées suivent le facteur de leur icône ; les positions et la visibilité ne changent pas. Cela réduit certains chevauchements, sans promettre de séparer des icônes exactement superposées.

Le raccourci utilise le service [IKeyState](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IKeyState.cs), la capture ImGui et [AtkModule.IsTextInputActive](https://github.com/aers/FFXIVClientStructs/blob/d8633414de71407f9eb45da830472e6e0fe26a08/FFXIVClientStructs/FFXIV/Component/GUI/AtkModule.cs). Le contrôle de fenêtre au premier plan est une lecture Win32 du PID, sans accès à un autre processus. Le coefficient temporaire alimente les routines de zoom et de portée existantes ; leurs bornes et leur périmètre restent inchangés. Cette version n’ajoute pas de règle PvP ni de liste blanche d’icônes à la collecte étendue.
