# FFXIV Processed Pipeline (Source ➜ Prefabs ➜ Addressables)

## Goals
- Keep raw FBX/texture sources outside `Assets` to minimize import time.
- Emit stable prefabs/materials into `Assets/FFXIV_Processed` for runtime loading.
- Remove FBX assets after processing so the project runs without any `.fbx` files under `Assets`.
- Use Addressables labels per category for fast, async loading.

## Source + Output
- Source root (external): `C:/Users/fmend/Desktop/TEST-EXTRACT-HUMAN-MODELS/chara`
- Staging import: `Assets/FFXIV_Imported`
- Processed output: `Assets/FFXIV_Processed/{Hair|Face|Equipment|Weapons}/<PrefabName>/`
- Each prefab folder contains:
  - `<PrefabName>.prefab`
  - `Materials/` (materials + `Textures/` copied from staging)
  - `Meshes/` (baked meshes extracted from FBX)
- After processing, staging can be cleaned (removes all `.fbx` files from the project) so only prefabs/materials/meshes remain in `Assets`.

## Editor Build Steps
1) **Processed Prefabs build**
   - Menu: `Tools/FFXIV/Processed Prefabs/Build from External Source...`
   - Copies FBX + textures from Source into staging.
   - Applies `FfxivImportProfile` and `FfxivMaterialPolicy`.
   - Bakes prefabs/materials/meshes into processed output.
   - Optional (default **ON**):
     - Auto Addressables setup (groups + labels).
     - Auto Addressable Index build.
     - Cleanup staging (removes `Assets/FFXIV_Imported` so the project has **no FBXs**).

2) **Addressables grouping**
   - Menu: `Tools/FFXIV/Addressables/Setup Groups (Processed Prefabs)`
   - Groups:
     - `FFXIV/Hair`, `FFXIV/Face`, `FFXIV/Equipment`, `FFXIV/Weapons`
   - Labels:
     - `Hair`, `Face`, `Equipment`, `Weapons`, `FFXIV`, `Body`
   - Addressable key: prefab name (example: `c0201h0142_hir`)
   - For automated builds, the processed prefabs step can run this without dialogs.

3) **Build**
   - `FfxivProcessedBuildStep` runs before player builds if a Source root is set.
   - For Addressables content builds, run the processed build and Addressables build.
   - To keep the project FBX-free, leave cleanup enabled (default) so staging is removed after the processed build.

## Runtime (Hybrid)
- Base bodies are loaded via Addressables using the `Body` label (equipment prefabs containing `e0000`).
- Hair/face are loaded via Addressables using labels `Hair` and `Face`.
- If Addressables are disabled for a category, `CharacterCreator` falls back to scene lists for that category.

## Key Scheme
- Addressables address = prefab file name (no category prefix).
- Example key: `c0201h0142_hir`.
- Tokens are already configured in `CharacterCreator`:
  - Male: `c0101`, Female: `c0201`, Hair: `_hir`, Face: `_fac`, Base: `e0000`.

## Body Defaults
- Base body pieces live in `Assets/FFXIV_Processed/Equipment` and are labeled `Body` when the prefab name contains `e0000`.
- Defaults auto-load the full base set for male (`c0101e0000_*`) and female (`c0201e0000_*`) without UI controls.

## Notes
- Addressables package is required. After adding `com.unity.addressables` to the manifest, install via Package Manager if Unity prompts you.
- If Addressables are disabled, `CharacterCreator` falls back to scene lists for hair/face.
