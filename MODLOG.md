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

## Status
- [x] compiles (0 warnings)
- [ ] install BepInEx + plugin (waiting for user OK)
- [ ] in game: plugin loads, Harmony patches apply
- [ ] in game: blocks render with the master shader, place/break
- [ ] in game: TNT, chain reaction, crater in blocks
- [ ] in game: crater in level geometry (run `probe` first: readable meshes? static batch?)
- [ ] in game: creeper walks, fuses, explodes
- [ ] showcase clip, README, package
