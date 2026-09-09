# Official submission preparation

Based on release 0.5.1 for game client `2026.09.01.0000.0000`. This branch carries forward the earlier D17 build adaptation: Dalamud.NET.Sdk 15.0.0, Packager, committed locked dependencies and SDK library paths. Runtime C# code and the native compatibility checks are unchanged.

On 9 September 2026, Aleqsd confirmed personally testing 0.5.1 in FFXIV with the latest game update and that it works. The isolated D17 build passed locked restore and Release compilation with .NET 10.0.400 and Dalamud 15.0.3.3, without warnings or errors. The preparation task did not load this rebuilt DLL or replace the installed plugin. The generated manifest retains API 15, version 0.5.1.0 and synchronous native lifecycle requirements.

## Scope still awaiting advice

The plugin permits zoom 0.25–2 rather than the native 0.5–2. Below 0.5 it extends native minimap marker collection up to twice the normal range, using the existing client marker vector and keeping the 100-marker limit. It does not change the world camera or add server queries. There is no PvP disable or safe-category allowlist for that extension; enemy markers are not guaranteed to be excluded. Client-side data does not establish the absence of a combat advantage.

Approval advice must precede a D17 submission for this scope. This build preparation is not acceptance. See [native mapping](native-mapping.md) and [release validation](validation.md) for restoration, compatibility and testing details.

AI usage: Auto (OpenAI Codex). Aleqsd chose features, tested in game and guided improvements; Codex wrote most of the code, including autonomous passes. The icon, square mask and procedural frames were made with Codex and are declared in the plugin description. Aleqsd is happy to redraw the installer icon by hand if needed. Existing release assets and the custom catalogue are unchanged.
