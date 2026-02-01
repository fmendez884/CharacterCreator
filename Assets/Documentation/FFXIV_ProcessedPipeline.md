# FFXIV Processed Pipeline (Source ➜ Prefabs ➜ Addressables)

## Goals
- Keep raw FBX/texture sources outside `Assets` to minimize import time.
- Emit stable prefabs/materials into `Assets/FFXIV_Processed` for runtime loading.
- Use Addressables labels per category for fast, async loading.

## Source + Output
- Source root (external): `C:/Users/fmend/Desktop/TEST-EXTRACT-HUMAN-MODELS/chara`
- Staging import: `Assets/FFXIV_Imported`
- Processed output: `Assets/FFXIV_Processed/{Hair|Face|Equipment|Weapons}/<PrefabName>/`
- Each prefab folder contains:
  - `<PrefabName>.prefab`
  - `Materials/` (materials + `Textures/` copied from staging)
  - `Meshes/` (baked meshes extracted from FBX)

## Editor Build Steps
1) **Processed Prefabs build**
   - Menu: `Tools/FFXIV/Processed Prefabs/Build from External Source...`
   - Copies FBX + textures from Source into staging.
   - Applies `FfxivImportProfile` and `FfxivMaterialPolicy`.
   - Bakes prefabs/materials/meshes into processed output.

2) **Addressables grouping**
   - Menu: `Tools/FFXIV/Addressables/Setup Groups (Processed Prefabs)`
   - Groups:
     - `FFXIV/Hair`, `FFXIV/Face`, `FFXIV/Equipment`, `FFXIV/Weapons`
   - Labels:
     - `Hair`, `Face`, `Equipment`, `Weapons`, `FFXIV`
   - Addressable key: prefab name (example: `c0201h0142_hir`)

3) **Build**
   - `FfxivProcessedBuildStep` runs before player builds if a Source root is set.
   - For Addressables content builds, run the processed build and Addressables build.

## Runtime (Hybrid)
- Base bodies still auto-collected from scene using `CharacterCreator.AutoCollectFromScene`.
- Hair/face are loaded via Addressables using labels `Hair` and `Face`.
- Filtering works against the Addressables key strings (same tokens as the original names).

## Key Scheme
- Addressables address = prefab file name (no category prefix).
- Example key: `c0201h0142_hir`.
- Tokens are already configured in `CharacterCreator`:
  - Male: `c0101`, Female: `c0201`, Hair: `_hir`, Face: `_fac`.

## Notes
- Addressables package is required. After adding `com.unity.addressables` to the manifest, install via Package Manager if Unity prompts you.
- If Addressables are disabled, `CharacterCreator` falls back to scene lists for hair/face.
