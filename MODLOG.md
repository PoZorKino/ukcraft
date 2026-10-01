# UKCraft — MODLOG

Minecraft-style building in ULTRAKILL + creepers and TNT that destroy the world. Started 2026-10-01
(the co-op idea from earlier the same day was scrapped; notes in ~/ultrakill-coop/MODLOG.md, nothing was installed).

## Facts
- Game: ULTRAKILL, Steam 1229490, `D:\!stem\steamapps\common\ULTRAKILL`, buildid 22957324, Unity 2022.3.29f1 Mono x64, no anti-cheat.
- Saves and prefs are IN the game folder (`Saves\`, `Preferences\`). Player.log: `%USERPROFILE%\AppData\LocalLow\Hakita\ULTRAKILL\`.
- Backups: `~/.universal-modder/backups/ultrakill-saves/20261001-095949.zip`, `.../ultrakill-prefs/20261001-095949.zip` (`um backup restore`).
- Decompile (never commit): `~/ultrakill-decomp` (ilspycmd 11.1 of Assembly-CSharp.dll).
- Loader: BepInEx 5.4.23.5 x64, zip at `~/ultrakill-coop/dl/`. NOT installed yet (needs the user's OK).

## Route
Pattern 1 (port the content) as a BepInEx 5 plugin, C#, Harmony. All textures are drawn in code (`Blocks.cs`): no Minecraft files.

## What the game code says (decompile)
- Layers: Environment mask = 6 (OutdoorsBaked), 7 (EnvironmentBaked), 8 (Environment), 24 (Outdoors). Enemies 10/11 (+12 big). Player layer 2.
- `MonoSingleton<T>.Instance` auto-creates unless the type has NoAutoInstance; all the ones the mod uses have it.
- Level rendering: `StaticSceneOptimizer` swaps every static MeshRenderer's mesh for a big baked mesh and calls
  `SetStaticBatchInfo(renderer, firstSubMesh, 1)`; mapping is `staticMRends[i]` -> `bakedDataAsset.firstSubMesh[i]`. Baked layer = 7/6.
  Older `LucasMeshCombine` makes readable world-space "Combined Mesh" objects and disables the source renderers.
- `Explosion` (prefabs `DefaultReferenceManager.explosion` / `superExplosion`): Start() multiplies maxSize by globalSizeMulti;
  breaks `Breakable`s it touches via `Break(damage)`.
- Weapons break `Breakable` (weak) on hit -> TNT blocks carry a Breakable + TntMarker and a Harmony prefix on `Breakable.Break(float)` lights them.
- Filth = `Enemy` + `ZombieMelee` (Swing / JumpAttack / DiveCheck), NavMeshAgent at `Enemy.nma`. Creeper = cloned Filth, body hidden.
- Leaderboards: `LeaderboardController.SubmitLevelScore/SubmitCyberGrindScore/SubmitFishSize` -> blocked by the mod.

## Layout
- `src/Plugin.cs` config + bootstrap, `Blocks.cs` 36 blocks + atlas, `VoxelWorld.cs` chunks/meshing, `BuildMode.cs` hotbar + input,
  `PrimedTnt.cs`, `Destruction.cs` craters, `LevelCarver.cs` cuts holes in level meshes/colliders, `Creeper.cs`, `Patches.cs`,
  `Commands.cs` test channel (`BepInEx/config/ukcraft.cmd`, one command per line), `Probe.cs` scene dump.
- Build: `dotnet build UKCraft.csproj -c Release` -> `bin/Release/netstandard2.1/UKCraft.dll` (only that one file ships).

## Shader (read from the game's asset bundles with UnityPy, 2026-10-01)
- Level shader is `ULTRAKILL/Master` (in `StreamingAssets/aa/StandaloneWindows64/assets_assets_assets/shaders.bundle`), one pass, LIGHTMODE Vertex.
  Most materials use keywords `VERTEX_LIGHTING _FOG_ON` (1177 of 2144); `_FOG_ON` alone exists too (43). Props: `_MainTex`, `_Color`,
  `_VertexColors` (default 1), `_CullMode` 2, `_ZWrite` 1.
- Block material = Master + `_FOG_ON` (+ `VERTEX_LIGHTING` if config LitByLevel). Fallback `Unlit/Texture` (it is in resources.assets).
- Stencil is only set for layer 24 (Outdoors) by `StencilValuesByLayer`; blocks are on layer 8.

## Tests
- `tests/CutterTest` (`dotnet run -c Release`): runs the real `src/Cutter.cs` against stand-in Vector3 types. Checks hole area on a
  floor lying on a grid plane, floor/ceiling ownership, and 400 random triangles against point sampling. Found and fixed one bug
  (surface exactly on the outer face of the blast box was never cut -> box now has a 0.05 margin).

## Install state (2026-10-01)
- User chose "install, I'll test myself": do NOT launch or drive the game unless they say so.
- BepInEx 5.4.23.5 x64 unzipped into the game folder (winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt, BepInEx/).
- Plugin at `BepInEx/plugins/UKCraft/UKCraft.dll`. Redeploy: `sh tools/deploy.sh`.
- Uninstall: delete those five items from the game folder.
- Log to read after a session: `<game>/BepInEx/LogOutput.log` (lines "carved N cells: ...", "can't carve ...", exceptions).

## Status
- [x] compiles (0 warnings), cutter unit test passes
- [x] BepInEx + plugin installed
- [ ] in game (user testing): plugin loads, Harmony patches apply
- [ ] in game: blocks render with the master shader, place/break
- [ ] in game: TNT, chain reaction, crater in blocks
- [ ] in game: crater in level geometry (static-batched renderers are the uncertain part; `probe` command dumps the scene)
- [ ] in game: creeper walks, fuses, explodes
- [ ] showcase clip, README, package
