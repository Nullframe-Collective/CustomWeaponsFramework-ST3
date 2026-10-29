# CustomWeaponsFramework 1.3.0

The framework binds player and weapon components once and retains their references. It does not use `Transform.Find`, `FindChild` or a global player search to resolve the player rig or inventory.

`WeaponReferences` reads the arm renderer from `SyncCustomization.DCBPBGEFBHD`, character animation from `PlayerNetworkController.ODDPEBHLOBE`, and the third-person fire point from the native `WeaponSync` components under `PlayerNetworkController.HDCJMMEMDEJ`. These are the game's serialized references. Renaming or reparenting bones does not change the binding.

The FPS templates still use AKM Rifle, Grenade Launcher, MCS870 and Knife. They are selected by `WeaponScript.JIFANOLAADI` and checked against `PFGPKNNMAHG`, rather than found by transform name. Selecting the first script of a type would incorrectly include Camera as a machine gun and Flashlight as a knife. Changes to these native game IDs or component fields in a future game build require updating the adapter.

`WeaponInventory` keeps the native scripts and custom FPS models in a per-local-player cache. `Grant`, `Select`, `Holster`, `ViewModel` and readiness checks use that cache, including both native weapon IDs and their existing object-name aliases. A new local manager or game scene clears the cache. Remote player initialization does not bind the local inventory.

The public catalog/inventory API remains version 1. Existing weapon.json files and bundles retain their format. Optional `MuzzleFlash` and `PointLight` markers inside a weapon bundle keep their existing names; the framework resolves them during construction. The TPS muzzle renderer may be nested. These bundle markers are separate from the game's player hierarchy.

Other fixes:

- Inactive renderers are included when configuring scope and fur references.
- Positive `Fractions` and `WaitBeforeReload` values now configure the shotgun and grenade launcher. Missing/default values preserve the template settings for these fields.
- Weapon construction releases its initialization guard when interrupted and skips already registered custom models on retry.
- The loader avoids duplicate Bullet components and repeated loading of cached icons/crosshairs/scopes, disposes WWW requests, and reports invalid definitions without aborting initialization.
- A missing muzzle no longer leaves a machine gun referencing a destroyed template object. WorldModel rejects incomplete prefab arrays.

Install `CustomWeaponsFramework.dll` in Mods on the host and clients, then restart the game. This release works with the current NullServers integration; no NullServers source or network packet changes are required.

Validation on game 2.625 / Unity 6000.0.82f1 / MelonLoader 0.7.3:

- Release build: zero errors and warnings.
- A disabled copy of NetworkPlayer2 with every transform renamed still resolves its manager, animation, arm, four exact native templates and native TPS fire point.
- In a private NullFrame room, both installed custom tool models remain accessible after weapon/model renaming; repeated initialization creates no duplicates.
- PhysGun and ToolGun selection, holstering, cube placement and the ToolGun duplicate preview were exercised in game.

A two-computer connection, ordinary custom gun firing and the offline spawn path have not been exercised with this release. The installed test content contained PhysGun and ToolGun. Temporary diagnostics are excluded from the release archives.
