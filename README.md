# Minimap Zoom

Dézoomez davantage sur la mini-carte de FFXIV, choisissez une forme carrée et personnalisez son cadre. Masquez les marqueurs ou la décoration soleil/lune, et réglez la taille des icônes.

![Réglages de Minimap Zoom](docs/images/settings-map.png)

*Panneau réel du plugin rendu avec ImGui hors jeu, police Segoe UI. Le rendu natif de la mini-carte reste à tester en jeu.*

## Installation

1. Télécharger puis extraire le ZIP de la [release](https://github.com/aleqsd/minimap-zoom/releases).
2. Dans `/xlsettings` → **Experimental** → **Dev Plugin Locations**, ajouter le chemin complet de `MinimapZoom.dll`.
3. Conserver `MinimapZoom.json` à côté de la DLL. Dans `/xlplugins`, scanner les plugins de développement et activer **Minimap Zoom**.
4. Ouvrir `/minizoom` et sélectionner le zoom dans **Carte**.

Pour une mise à jour, remplacer l’ancienne version et garder une seule entrée dans Dev Plugin Locations. `/minizoom off` restaure la mini-carte. Le zoom automatique se choisit dans **Démarrage**.

## Réglages

- **Carte** : zoom de 0,25 à 2, forme, cadre et soleil/lune.
- **Marqueurs** : tailles séparées, masquage global ou par catégorie. Votre personnage reste visible.
- **Style** : LMeter, Obsidienne ou Dalamud ; police, couleurs et disposition de la fenêtre. Expressway est utilisée si elle est installée, avec repli Dalamud.

**Version expérimentale 0.4.0**, pour Dalamud API 15 et le client `2026.08.11.0000.0000`. La portée des icônes fixes suit le dézoom ; la limite native de 100 marqueurs et les données disponibles côté client restent applicables. Les anciennes configurations conservent l’apparence Dalamud jusqu’au choix d’un autre habillage.

[Compiler et vérifier](docs/development.md) · [Validation](docs/validation.md) · [Historique](CHANGELOG.md)
