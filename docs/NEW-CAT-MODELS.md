# Five user model replacements — 2026-11-09

The source GLBs in Downloads remain unchanged. Replacement is selected by stable breed ID in `ImportedCatCatalog`; both Boutique previews and saved residents resolve the same appearance. Names, personalities, memories, relationships and household save schema are unchanged.

| Source GLB | Resident / breed ID | Unity resource |
| --- | --- | --- |
| `0dbeb6b2d88e8afc02e0d4255e21b8ef.glb` | Cotton / 7 | `ImportedCats/Ragdoll/Model` |
| `347b20c634490440591f7ae022bdb77d.glb` | Yuki / 4 | `ImportedCats/White/Model` |
| `2275b94f3443280411ad1720d3c0d705.glb` | Shadow / 5 | `ImportedCats/Black/Model` |
| `textured.glb` | Earl / 1 | `ImportedCats/BritishBlue/Model` |
| `img23d_33972.glb` | Mochi / 6 | `ImportedCats/Calico/Model` |

Each source is an unrigged static GLB. The local pipeline reduces it to about 40,000 triangles, preserves source UVs, resizes PBR textures to 2048, binds 27 bones with at most four influences, and retargets the project's existing eight procedural prototype clips. Isolated whisker/surface fragments are excluded from the voxel heat solver and receive transferred weights from the connected proxy. These are prototype animations, not mocap or production retopology; facial blend shapes are absent.

`python scripts/model-tools/prepare_new_cats.py` requires bpy, numpy and scipy, the listed source files, and `artifacts/imported-cat/Tabby-editable.blend`. It can regenerate the reduced caches when absent. Per-model source hashes and processing reports are in `artifacts/new-cats/{BritishBlue,Ragdoll,White,Black,Calico}/processing-report.json`. Unity's asset processor prepares Legacy clips, shared fur materials and iOS ASTC textures.

The appearance setting shows the breed replacement for these five residents; its “看看” action opens that resident's Boutique preview. Other breeds retain the previous appearance toggle. Yuki and Shadow's appearance descriptions now match the supplied amber/golden eyes.

Historical validation (2026-10-22; see current validation below):

- 65 core tests passed.
- macOS Unity Player build passed; iOS-conditional C# compilation passed.
- `--new-cats-review`: the original four replacements passed 32 rendered poses and five-cat save/reload.
- `--boutique-review`: 36 previews and profile open/return checks, pet/stretch, UI containment, adoption and reopen passed at the batch Player's actual 640×480 resolution. This batch run does not establish four real screen sizes.
- Updated five-cat Player regression passed: production collider selection, doorway traffic, sniffing, shared food and save/resume.
- Unity iOS export and signed iPhone Release build passed (`artifacts/new-cats/ios-build.log`); updated app installed on the connected iPhone (`iphone-install.json`).
- Desktop rendered comparison: `artifacts/new-cats/preview.jpg`.

The remote launch attempt was rejected because the iPhone was locked (`iphone-launch.json`); on-device rendering and touch were not confirmed.

## 2026-11-09 asset validation

Earl's `textured.glb` and Mochi's `img23d_33972.glb` were converted with the local pipeline. Each output has 40,000 triangles, 27 bones, zero unbound heat-proxy vertices, and the source 2048px base-color texture. Lux source meshes are grounded, scaled to the template's 0.72m rest height, and oriented before heat binding.

Unity licensing was recovered by restarting its unresponsive IPC service. macOS build and iOS export passed. The current `--new-cats-review` passed all five Boutique replacements, 40 rendered poses, finite/bounded deformation, identity-preserving resident rebuilds and five-cat save/reload. Evidence: `artifacts/new-cats/current-player.log`, `artifacts/new-cats/player/PASS.txt` and the rendered images in that folder. The latest core run passed 107 tests. iOS build number is 18; device installation is conditional on the physical iPhone reconnecting.

The physical iPhone reconnected and build 18 was installed successfully using the existing bundle identifier. Device app inventory confirms 0.3.0 (18). All six save files were backed up and remained byte-identical after installation, before first launch. iOS denied the remote launch because the phone had locked; actual device rendering and touch remain unverified. Evidence: `artifacts/new-cats/iphone18-{install,app-info,launch,status}.json` and the two `iphone18-saves-*` directories.

Device build/install evidence is recorded separately in `artifacts/new-cats`. A successful build or launch does not establish physical touch quality, subjective animation naturalness or sustained thermal performance.
