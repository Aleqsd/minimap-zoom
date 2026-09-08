# Compatibilité du client de septembre

Vérification statique du 8 septembre 2026 pour Minimap Zoom 0.5.1. Le client installé annonce `2026.09.01.0000.0000`. Son exécutable a pour SHA-256 :

```text
C8CA32A9332924E19EC0FCA158ADC9A783656DD0A89D84168C0604476BB7640B
```

La 0.5.0 refuse ce client avant tout scan ou création de hook : elle vérifie explicitement le client d’août et son empreinte. Ce blocage est attendu lorsqu’une version n’a pas encore été analysée. La 0.5.1 accepte uniquement le nouveau couple version/empreinte vérifié ; elle ne remplace pas ce contrôle par une acceptation générale.

## Sources et fonctions

Les [symboles FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/blob/21898bf815f0e56e02b7dc08f0a3e24822c759d0/ida/data.yml) annoncent la même version. Ils identifient notamment `AgentHUD.UpdateNaviMap` et le constructeur de `AddonNaviMap`. L’inspection Iced de l’exécutable sur disque confirme les autres fonctions par leurs signatures et leurs instructions.

| Fonction | RVA actuelle | RVA précédente |
| --- | --- | --- |
| ApplyZoom | `0x1582980` | `0x157BBF0` |
| RefreshMarker | `0x1582C80` | `0x157BEF0` |
| RefreshMap | `0x15830B0` | `0x157C320` |
| AgentHUD.UpdateNaviMap | `0xFE4C00` | `0xFE0460` |
| CollectMapMarkers | `0xFC1390` | `0xFBCBF0` |

Les cinq signatures existantes sont uniques dans la section `.text`. Les adresses servent au contrôle statique ; le plugin continue à utiliser le scanner Dalamud.

- ApplyZoom conserve les bornes 0,5–2, les champs `+0x3A78` et `+0x264`, le paramètre `byte refresh`, le parcours de 100 marqueurs et les appels aux deux routines de rendu. Les instructions comparées correspondent après relocation.
- RefreshMarker conserve les groupes de bordure à `+0x25C0`, le pas de `0x30` et les éléments utilisés par le masquage. Ses instructions de traitement correspondent après relocation ; la table de branchement est déplacée.
- RefreshMap utilise toujours `NaviMap +0x238`, `MapBase +0x15D8` et `MapImage +0x15E0`. La transformation `Atk2DNaviMap` appelée est à `0x123DF40`. Elle conserve le traitement des surfaces de type 12 et du bit `0x1000`.
- UpdateNaviMap calcule toujours `175 / MiniMapScale` puis son carré aux champs `HUD +0x4E0C/0x4E10` (`0xFE5198–0xFE51E5`). La collecte à `0xFE5210` reçoit le vecteur `HUD +0x4B30`, le drapeau mini-carte et le rayon 200. La limite de 100 marqueurs et le filtrage de distance sont conservés (`0xFE5340–0xFE53A9`).
- CollectMapMarkers conserve son ABI et la comparaison avec `(radius + taille)^2` (`0xFC1972–0xFC19BB`). Les tables de branchement et références d’adresses relocalisées ne sont pas interprétées comme des changements de logique.

## Structures et affichage

Les contrôles `NativeLayout.Validate` passent avec les bibliothèques Dalamud 15.0.3.3 : tailles et offsets de tous les champs vérifiés restent identiques. Le fichier `ui/uld/navimap.uld` a été relu via Lumina. Son `WidgetData`, avec les 19 nœuds, est exactement identique au relevé du client d’août, y compris géométrie, types, relations et drapeaux.

La lecture du constructeur et de l’initialisation confirme notamment le zoom initial `0,75`, le bouton de dézoom, le soleil et la collision. `AtkResNode.SetAlpha` à `0x660600` conserve l’écriture du seul octet `+0x83`. Le changement ne modifie ni les préférences, ni les mécanismes de restauration.

Le diagnostic mémorise désormais la version réellement lue lors du chargement et la distingue de celle prise en charge. Un compteur de portée indisponible n’est plus présenté comme zéro. Les erreurs gardent les effets désactivés et les réglages conservés.

## Niveau de preuve

Compilation et 70 contrôles hors jeu réussis, dont l’empreinte, les cinq signatures et les refus d’une version ou d’une empreinte inattendue. Les rendus du diagnostic ont été inspectés aux tailles normale/minimale et à 100 %, 150 % et 200 %. Les relevés locaux restent sous `artifacts/compat-20260908/`, hors distribution ; aucun fichier extrait du jeu n’est publié.

Ces vérifications lisent le client sur disque et utilisent des allocations isolées. Elles ne prouvent pas le rendu, les clics, les hooks actifs ou la restauration en jeu. Charger une seule copie de 0.5.1, puis vérifier zoom, carré, masquage, portée, changement de zone, éditeur ATH et désactivation reste nécessaire.

## Pourquoi Codex Monitor n’a pas eu le même blocage

Minimap Zoom intercepte et modifie l’interface native de FFXIV, avec un contrat précis sur son binaire. Codex Monitor dessine ses fenêtres via les services Dalamud et lit les données de son relais local. Son code ne possède pas ce contrôle de version de l’exécutable ni ces hooks de mini-carte. Une mise à jour compatible avec ses services Dalamud ne nécessite donc pas le même correctif ; cela ne garantit pas sa compatibilité avec toute future API Dalamud ou évolution du relais.
