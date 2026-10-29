using Il2Cpp;
using MelonLoader;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnityEngine;

[assembly: MelonInfo(typeof(CustomWeaponsFramework.Core), "CustomWeaponsFramework", "1.3.7", "Nullframe Collective")]
[assembly: MelonGame("ZeoWorks", "Slendytubbies 3")]
[assembly: MelonColor(255, 239, 68, 68)]

namespace CustomWeaponsFramework
{
    public class Core : MelonMod
    {
        private string _customWeaponsDirectory = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "CustomWeapons");
        public static List<WeaponInfo> LoadedWeapons = new();
        public static bool IsInitialized { get; private set; }
        public static bool AssetsReady { get; private set; }
        public static string Error { get; private set; }
        private static readonly HashSet<IntPtr> CreatingWeapons = new();
        private static readonly HashSet<IntPtr> CreatedPlayers = new();
        public static Dictionary<string, GameObject[]> LoadedBundles = new();
        public static Dictionary<string, GameObject> LoadedProjectiles = new();
        public static GameObject WeaponRoot;
        public static GameObject[] WeaponTemplates;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
            InitializeWeaponsSetup();
        }

        public override void OnUpdate() => WeaponInventory.Update();

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName is "Updater" or "MainMenu") return;

            WeaponRoot = null;
            WeaponInventory.Reset();
            WeaponTemplates = null;
            CreatingWeapons.Clear();
            CreatedPlayers.Clear();
            AssetsReady = false;

            if (WeaponCatalog.Enabled)
            {
                LoadBundles();
            }
        }

        private void InitializeWeaponsSetup()
        {
            Directory.CreateDirectory(_customWeaponsDirectory);
            LoadedWeapons.Clear();
            HashSet<string> ids = new(StringComparer.Ordinal);
            HashSet<string> names = new(StringComparer.Ordinal);

            foreach (string directory in Directory.GetDirectories(_customWeaponsDirectory))
            {
                if (!File.Exists(Path.Combine(directory, "weapon.json")) && !File.Exists(Path.Combine(directory, "bundle"))) continue;

                try
                {
                    WeaponInfo weapon = ReadWeapon(directory, "legacy:" + Path.GetFileName(directory).ToLowerInvariant());
                    if (!ids.Add(weapon.Id) || !names.Add(weapon.Name))
                    {
                        throw new InvalidDataException("Duplicate or invalid weapon ID/name.");
                    }

                    LoadedWeapons.Add(weapon);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
                {
                    Error = $"Cannot load weapon {Path.GetFileName(directory)}: {exception.Message}";
                    LoggerInstance.Error(Error);
                }
            }

            LoadedWeapons.Sort((first, second) => StringComparer.Ordinal.Compare(first.Id, second.Id));
            IsInitialized = true;
            WeaponCatalog.OnReady();
        }

        internal static WeaponInfo ReadWeapon(string directory, string fallbackId)
        {
            string json = File.ReadAllText(Path.Combine(directory, "weapon.json"));
            WeaponInfo weapon = JsonSerializer.Deserialize<WeaponInfo>(json);

            if (weapon == null ||
                string.IsNullOrWhiteSpace(weapon.Name) ||
                !Enum.IsDefined(typeof(WeaponScript.DOEEBEFKIJI), weapon.Type) ||
                !Enum.IsDefined(typeof(AnimationType), weapon.AnimationType))
            {
                throw new InvalidDataException("Invalid weapon definition.");
            }

            weapon.Id = string.IsNullOrEmpty(weapon.Id) ? fallbackId : weapon.Id;
            if (!WeaponCatalog.ValidId(weapon.Id))
            {
                throw new InvalidDataException("Invalid weapon ID.");
            }

            using FileStream bundle = File.OpenRead(Path.Combine(directory, "bundle"));
            using SHA256 algorithm = SHA256.Create();
            string hash = Convert.ToHexString(algorithm.ComputeHash(bundle));

            weapon.ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash + "\n" + json.Replace("\r\n", "\n"))));
            weapon.Path = directory;

            return weapon;
        }

        internal static void Forget(string name)
        {
            LoadedBundles.Remove(name);
            LoadedProjectiles.Remove(name);
        }

        internal static void Refresh()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!WeaponCatalog.Enabled || scene is "Updater" or "MainMenu") return;

            LoadBundles();

            foreach (Volume volume in UnityEngine.Object.FindObjectsOfType<Volume>())
            {
                VolumePatch.Postfix(volume);
            }

            foreach (ShopSystem shop in UnityEngine.Object.FindObjectsOfType<ShopSystem>())
            {
                ShopSystemAwake.Postfix(shop);
            }

            foreach (PlayerNetworkController player in UnityEngine.Object.FindObjectsOfType<PlayerNetworkController>())
            {
                MelonCoroutines.Start(CreateFirstPersonWeapons(player.gameObject));
            }
        }

        private static void LoadBundles()
        {
            foreach (WeaponInfo weapon in LoadedWeapons)
            {
                if (weapon == null) continue;

                string bundlePath = Path.Combine(weapon.Path, "bundle");
                if (File.Exists(bundlePath) && NeedsBundle(weapon))
                {
                    LoadBundle(weapon, bundlePath);
                }
            }

            AssetsReady = Error == null && LoadedWeapons.All(weapon =>
                LoadedBundles.TryGetValue(weapon.Name, out var data) && data[0] && data[1]);
        }

        private static bool NeedsBundle(WeaponInfo weapon)
        {
            if (!LoadedBundles.TryGetValue(weapon.Name, out var loaded)) return true;
            if (loaded != null && loaded.Length >= 2 && loaded[0] && loaded[1]) return false;

            LoadedBundles.Remove(weapon.Name);

            return true;
        }

        private static void LoadBundle(WeaponInfo weapon, string bundlePath)
        {
            LoadedProjectiles.Remove(weapon.Name);
            Il2CppAssetBundle assetBundle = Il2CppAssetBundleManager.LoadFromFile(BundleCache.Resolve(bundlePath), 0, 0);
            if (assetBundle == null)
            {
                Error = $"Failed to load AssetBundle for weapon: {weapon.Name}";
                MelonLogger.Error(Error);

                return;
            }

            GameObject fpsPrefab = assetBundle.Load<GameObject>(weapon.Name);
            GameObject tpsPrefab = assetBundle.Load<GameObject>(weapon.Name + " (TPS)");
            GameObject projectilePrefab = assetBundle.Load<GameObject>(weapon.Name + " (Projectile)");

            if (!fpsPrefab || !tpsPrefab || !fpsPrefab.GetComponentInChildren<Animation>(true))
            {
                Error = $"Weapon {weapon.Name} requires FPS/TPS prefabs and a legacy Animation component.";
                MelonLogger.Error(Error);
                assetBundle.Unload(false);

                return;
            }

            LoadedBundles[weapon.Name] = [fpsPrefab, tpsPrefab, projectilePrefab];

            if (projectilePrefab != null)
            {
                if (!projectilePrefab.GetComponent<Bullet>())
                {
                    projectilePrefab.AddComponent<Bullet>();
                }

                projectilePrefab.hideFlags = HideFlags.HideAndDontSave;
                LoadedProjectiles[weapon.Name] = projectilePrefab;
            }

            weapon.ShotSound = assetBundle.Load<AudioClip>("fire");
            weapon.ReloadSound = assetBundle.Load<AudioClip>("reload");
            assetBundle.Unload(false);
        }

        public static IEnumerator CreateFirstPersonWeapons(GameObject player)
        {
            yield return new WaitForSeconds(0.05f);

            if (!WeaponCatalog.Enabled || !player) yield break;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!AssetsReady && Error == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!AssetsReady || !player) yield break;

            PlayerNetworkController controller = player.GetComponent<PlayerNetworkController>();
            while (controller && !controller.IsClientInitialized && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (controller && !controller.IsClientInitialized) yield break;

            if (controller && !controller.IsOwner)
            {
                yield return CreateThirdPersonWeapons(controller);
                yield break;
            }

            yield return MelonCoroutines.Start(LoadWeaponTextures());

            if (!WeaponCatalog.Enabled || !player || controller && (!controller.IsClientInitialized || !controller.IsOwner)) yield break;

            WeaponReferences references = new(player, controller);
            WeaponManager manager = references.Manager;
            WeaponRoot = manager ? manager.gameObject : null;

            if (WeaponRoot == null)
            {
                MelonLogger.Error("Cannot instantiate weapons: WeaponRoot not found");
                yield break;
            }

            WeaponInventory.Bind(manager);

            if (HasWeapons())
            {
                yield return CreateThirdPersonWeapons(controller);
                yield break;
            }

            if (!CreatingWeapons.Add(WeaponRoot.Pointer)) yield break;

            IntPtr root = manager.Pointer;
            try
            {
                WeaponTemplates = references.Templates;

                foreach (WeaponInfo weapon in LoadedWeapons)
                {
                    if (weapon == null || WeaponInventory.ViewModel(weapon.Id)) continue;

                    if (!player || !manager || WeaponRoot != manager.gameObject ||
                        controller && (!controller.IsClientInitialized || !controller.IsOwner)) yield break;

                    (WeaponAnimation animation, WeaponScript script) = CreateViewModel(weapon, manager, references);
                    if (script != null)
                    {
                        yield return InitializeWeaponAnimation(animation, script);
                    }
                }
            }
            finally
            {
                CreatingWeapons.Remove(root);
            }

            WeaponInventory.OnReady();
            CreateOtherPlayersWeapons(controller);
            yield return CreateThirdPersonWeapons(controller);
        }

        private static (WeaponAnimation Animation, WeaponScript Script) CreateViewModel(WeaponInfo weapon, WeaponManager manager, WeaponReferences references)
        {
            int typeIndex = (int)weapon.Type;
            if (typeIndex < 0 || typeIndex >= WeaponTemplates.Length || WeaponTemplates[typeIndex] == null) return default;

            if (!LoadedBundles.TryGetValue(weapon.Name, out GameObject[] bundleData) || bundleData == null || bundleData.Length == 0 || bundleData[0] == null)
            {
                MelonLogger.Warning($"Cannot instantiate FPS weapon '{weapon.Name}': prefab not found");
                return default;
            }

            GameObject dummyWeapon = GameObject.Instantiate(WeaponTemplates[typeIndex], manager.transform, false);
            dummyWeapon.SetActive(false);
            dummyWeapon.name = weapon.Name;
            RemoveTemplateModel(dummyWeapon);

            GameObject model = GameObject.Instantiate(bundleData[0], dummyWeapon.transform, false);
            model.name = "Model";
            KeepVisible(model);
            Transform[] nodes = model.GetComponentsInChildren<Transform>(true).ToArray();
            Transform muzzleFlash = nodes.FirstOrDefault(node => node.name == "MuzzleFlash");
            Transform pointLight = nodes.FirstOrDefault(node => node.name == "PointLight");
            dummyWeapon.transform.localPosition = Vector3.zero;

            Animation animation = model.GetComponentInChildren<Animation>(true);
            if (!animation)
            {
                MelonLogger.Warning($"FPS prefab '{weapon.Name}' requires a legacy Animation component");
                GameObject.Destroy(dummyWeapon);
                return default;
            }

            WeaponAnimation weaponAnimation = animation.gameObject.AddComponent<WeaponAnimation>();
            ConfigureWeaponAnimation(weaponAnimation);

            if (dummyWeapon.GetComponent<WeaponSync_Catcher>() == null)
            {
                dummyWeapon.AddComponent<WeaponSync_Catcher>();
            }

            WeaponScript weaponScript = dummyWeapon.GetComponent<WeaponScript>();
            if (weaponScript == null)
            {
                MelonLogger.Warning($"WeaponScript not found on weapon '{weapon.Name}'");
                GameObject.Destroy(model);
                GameObject.Destroy(dummyWeapon);
                return default;
            }

            ConfigureWeapon(weaponScript, weapon);
            ConfigureWeaponType(weaponScript, weapon, dummyWeapon, model, muzzleFlash, pointLight);
            ConfigureCharacterAnimation(references.Animation, weaponScript, weapon);
            ConfigureAntenna(dummyWeapon, references.Arm);
            WeaponInventory.Register(weaponScript, model);

            return (weaponAnimation, weaponScript);
        }

        private static void RemoveTemplateModel(GameObject weapon)
        {
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
            {
                if (child.parent == weapon.transform)
                {
                    child.gameObject.SetActive(false);
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        private static void CreateOtherPlayersWeapons(PlayerNetworkController local)
        {
            foreach (PlayerNetworkController other in UnityEngine.Object.FindObjectsOfType<PlayerNetworkController>())
            {
                if (other != local)
                {
                    MelonCoroutines.Start(CreateThirdPersonWeapons(other));
                }
            }
        }

        private static void KeepVisible(GameObject model)
        {
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
            }
        }

        public static bool HasWeapons()
        {
            return WeaponRoot && LoadedWeapons.All(weapon => weapon != null && WeaponInventory.Find(weapon.Name));
        }

        public static IEnumerator LoadWeaponTextures()
        {
            foreach (WeaponInfo weapon in LoadedWeapons)
            {
                if (weapon == null || string.IsNullOrEmpty(weapon.Path)) continue;

                if (!weapon.Icon) yield return LoadTexture(Path.Combine(weapon.Path, "icon.png"), texture => weapon.Icon = texture);
                if (!weapon.Crosshair) yield return LoadTexture(Path.Combine(weapon.Path, "crosshair.png"), texture => weapon.Crosshair = texture);
                if (!weapon.Scope) yield return LoadTexture(Path.Combine(weapon.Path, "scope.png"), texture => weapon.Scope = texture);
            }
        }

        private static IEnumerator LoadTexture(string path, Action<Texture2D> onLoaded)
        {
            if (!File.Exists(path)) yield break;

            WWW www = new("file://" + path);
            try
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    MelonLogger.Error($"Failed to load texture at {path}: {www.error}");
                    yield break;
                }

                Texture2D texture = www.texture;
                if (texture != null)
                {
                    onLoaded?.Invoke(texture);
                }
            }
            finally
            {
                www.Dispose();
            }
        }

        private static void ConfigureWeaponAnimation(WeaponAnimation animation)
        {
            animation.EBJNIFNOGJA = "reload";
            animation.FEJAHOOPHPD = "take-in";
            animation.HPKHKABOFOO = "take-out";
            animation.IBHAFEOOCFC = "fire";
            animation.LFKDOIFADHK = "idle";
        }

        private static void ConfigureWeapon(WeaponScript weaponScript, WeaponInfo weapon)
        {
            weaponScript.JIFANOLAADI = weapon.Name;

            if (weapon.IsTool)
            {
                weaponScript.KKCIIEDNLEO = Vector3.zero;
            }

            weaponScript.FLIAJIAOBHA.aimPosition = new Vector3(weapon.AimPositionX, weapon.AimPositionY, weapon.AimPositionZ);
            weaponScript.FLIAJIAOBHA.playAnimation = weapon.AimAnimation;
            weaponScript.HHAMHPCAHNK = weapon.SingleFire;

            if (weapon.AimFov != 0)
            {
                weaponScript.FLIAJIAOBHA.toFov = weapon.AimFov;
            }

            weaponScript.APFMIOFJJNC.recoilPower = weapon.RecoilPower;
            weaponScript.APFMIOFJJNC.shakeAmount = weapon.ShakePower;
            weaponScript.MGFCKCEIEKM = weapon.Icon;
            weaponScript.HIHFCMAJBJK = weapon.Crosshair;
        }

        private static void ConfigureWeaponType(WeaponScript weaponScript, WeaponInfo weapon, GameObject dummyWeapon, GameObject model, Transform muzzleFlash, Transform pointLight)
        {
            switch (weapon.Type)
            {
                case WeaponScript.DOEEBEFKIJI.MACHINE_GUN:
                    ConfigureMachineGun(weaponScript, weapon, dummyWeapon, model, muzzleFlash, pointLight);
                    break;
                case WeaponScript.DOEEBEFKIJI.SHOTGUN:
                    ConfigureShotgun(weaponScript, weapon, muzzleFlash, pointLight);
                    break;
                case WeaponScript.DOEEBEFKIJI.GRENADE_LAUNCHER:
                    ConfigureGrenadeLauncher(weaponScript, weapon);
                    break;
                case WeaponScript.DOEEBEFKIJI.KNIFE:
                    ConfigureKnife(weaponScript, weapon);
                    break;
            }
        }

        private static void ConfigureMachineGun(WeaponScript weaponScript, WeaponInfo weapon, GameObject dummyWeapon, GameObject model, Transform muzzleFlash, Transform pointLight)
        {
            var machineGun = weaponScript.FMPLNFBLHKJ;
            machineGun.bulletsPerClip = weapon.BulletsPerClip;
            machineGun.clips = weapon.Clips;
            machineGun.fireRate = weapon.FireRate;
            machineGun.AimErrorAngle = weapon.AimRecoil;
            machineGun.NoAimErrorAngle = weapon.Recoil;
            machineGun.reloadSound = weapon.ReloadSound;
            machineGun.reloadTime = weapon.ReloadTime;
            machineGun.fireSound = weapon.ShotSound;
            machineGun.muzzleFlash = muzzleFlash ? muzzleFlash.gameObject : null;
            machineGun.pointLight = pointLight?.GetComponent<Light>();

            ConfigureSniperScope(dummyWeapon, model, weaponScript, weapon);

            if (LoadedProjectiles.TryGetValue(weapon.Name, out GameObject bullet) && bullet != null)
            {
                machineGun.bullet = ConfigureProjectile(machineGun.bullet, bullet.transform, weapon);
            }
        }

        private static void ConfigureSniperScope(GameObject dummyWeapon, GameObject model, WeaponScript weaponScript, WeaponInfo weapon)
        {
            if (!weapon.UseScope || weapon.Scope == null) return;

            SniperScope scope = dummyWeapon.AddComponent<SniperScope>();
            List<GameObject> renderObjects = new();

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderObjects.Add(renderer.gameObject);
            }

            scope.GKHNJCJJDCJ = new(renderObjects.ToArray());
            scope.MGEKFJPKEDI = weapon.Scope;
            scope.OMDBGECMMOD = weaponScript;
        }

        private static void ConfigureShotgun(WeaponScript weaponScript, WeaponInfo weapon, Transform muzzleFlash, Transform pointLight)
        {
            var shotgun = weaponScript.MMIKHOPCECE;
            shotgun.bulletsPerClip = weapon.BulletsPerClip;
            shotgun.clips = weapon.Clips;
            shotgun.fireRate = weapon.FireRate;

            if (weapon.Fractions > 0)
            {
                shotgun.fractions = weapon.Fractions;
            }

            shotgun.errorAngle = weapon.AimRecoil;
            shotgun.reloadSound = weapon.ReloadSound;
            shotgun.reloadTime = weapon.ReloadTime;
            shotgun.fireSound = weapon.ShotSound;

            weaponScript.FMPLNFBLHKJ.muzzleFlash = muzzleFlash != null && muzzleFlash.childCount > 0 ? muzzleFlash.GetChild(0).gameObject : null;
            weaponScript.FMPLNFBLHKJ.pointLight = pointLight?.GetComponent<Light>();

            if (LoadedProjectiles.TryGetValue(weapon.Name, out GameObject bullet) && bullet != null)
            {
                shotgun.bullet = ConfigureProjectile(shotgun.bullet, bullet.transform, weapon);
            }
        }

        private static void ConfigureGrenadeLauncher(WeaponScript weaponScript, WeaponInfo weapon)
        {
            var grenadeLauncher = weaponScript.MJIKHNADGAG;
            grenadeLauncher.ammoCount = weapon.Clips;
            grenadeLauncher.initialSpeed = weapon.ProjectileSpeed;
            grenadeLauncher.reloadTime = weapon.ReloadTime;
            grenadeLauncher.shotDelay = weapon.ShotDelay;

            if (weapon.WaitBeforeReload > 0f)
            {
                grenadeLauncher.waitBeforeReload = weapon.WaitBeforeReload;
            }

            grenadeLauncher.reloadSound = weapon.ReloadSound;
            grenadeLauncher.fireSound = weapon.ShotSound;
        }

        private static void ConfigureKnife(WeaponScript weaponScript, WeaponInfo weapon)
        {
            var knife = weaponScript.EKODJEHEFDE;
            knife.delayTime = weapon.HitDelay;
            knife.fireRate = weapon.FireRate;
            knife.fireSound = weapon.ShotSound;
            weaponScript.HHAMHPCAHNK = true;

            if (LoadedProjectiles.TryGetValue(weapon.Name, out GameObject bullet) && bullet != null)
            {
                knife.bullet = ConfigureProjectile(knife.bullet, bullet.transform, weapon);
            }
        }

        public static Transform ConfigureProjectile(Transform defaultProjectile, Transform customProjectile, WeaponInfo weapon)
        {
            if (defaultProjectile == null || customProjectile == null || weapon == null) return defaultProjectile;

            Bullet defaultBullet = defaultProjectile.GetComponent<Bullet>();
            Bullet customBullet = customProjectile.GetComponent<Bullet>();
            if (defaultBullet == null || customBullet == null) return defaultProjectile;

            foreach (GameObject effect in defaultBullet.FABFPAOEEHE)
            {
                if (effect != null && !customBullet.FABFPAOEEHE.Contains(effect))
                {
                    customBullet.FABFPAOEEHE.Add(effect);
                }
            }

            if (weapon.BulletDamage != 0) customBullet.CJKBHAHOLMJ = weapon.BulletDamage;
            if (weapon.BulletForce != 0) customBullet.HHBHJIDDGIL = weapon.BulletForce;
            if (weapon.BulledSpeed != 0) customBullet.DHHNMAIFIFB = weapon.BulledSpeed;
            if (weapon.BulletLifeTime != 0) customBullet.LNEDFJIMHDL = weapon.BulletLifeTime;

            return customProjectile;
        }

        private static void ConfigureCharacterAnimation(CharacterAnimation characterAnimation, WeaponScript weaponScript, WeaponInfo weapon)
        {
            if (characterAnimation == null) return;

            switch (weapon.AnimationType)
            {
                case AnimationType.OneHand:
                    characterAnimation.JAEHDJFGHEB.Add(weaponScript);
                    break;
                case AnimationType.TwoHandPistol:
                    characterAnimation.NKMALFJABNF.Add(weaponScript);
                    break;
                case AnimationType.TwoHandRifle:
                    characterAnimation.DOODHOGLOJO.Add(weaponScript);
                    break;
            }
        }

        private static void ConfigureAntenna(GameObject dummyWeapon, SkinnedMeshRenderer antennaRenderer)
        {
            if (antennaRenderer == null) return;

            foreach (Renderer renderer in dummyWeapon.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == null ||
                    !string.Equals(renderer.sharedMaterial.name, "FUR (INSTANCE)", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AntennaColor antenna = renderer.GetComponent<AntennaColor>() ?? renderer.gameObject.AddComponent<AntennaColor>();
                antenna.DCMJNPMPBIK = antennaRenderer;
            }
        }

        public static IEnumerator InitializeWeaponAnimation(WeaponAnimation weaponAnimation, WeaponScript weaponScript)
        {
            yield return new WaitForSeconds(0.1f);

            if (weaponAnimation == null || weaponScript == null) yield break;

            weaponScript.HANLPABBHKN = weaponAnimation;
            weaponScript.gameObject.SetActive(false);
        }

        public static IEnumerator CreateThirdPersonWeapons(PlayerNetworkController source)
        {
            yield return new WaitForSeconds(0.05f);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (WeaponCatalog.Enabled && source && (!source.HDCJMMEMDEJ || !HasWeapons()) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!WeaponCatalog.Enabled || !source || !source.HDCJMMEMDEJ || !HasWeapons()) yield break;
            if (!CreatedPlayers.Add(source.Pointer)) yield break;

            Transform playerRoot = source.HDCJMMEMDEJ;
            Transform firePoint = WeaponReferences.FirePoint(source);

            foreach (WeaponInfo weapon in LoadedWeapons)
            {
                if (weapon != null && !weapon.IsTool)
                {
                    CreateWorldModel(weapon, playerRoot, firePoint);
                }
            }
        }

        private static void CreateWorldModel(WeaponInfo weapon, Transform playerRoot, Transform firePoint)
        {
            if (!LoadedBundles.TryGetValue(weapon.Name, out var bundleData) || bundleData == null || bundleData.Length <= 1 || bundleData[1] == null) return;

            WeaponScript weaponScript = WeaponInventory.Find(weapon.Name);
            if (weaponScript == null) return;

            GameObject tpsWeapon = GameObject.Instantiate(bundleData[1], playerRoot, false);
            tpsWeapon.name = weapon.Name;
            KeepVisible(tpsWeapon);

            WeaponSync weaponSync = tpsWeapon.AddComponent<WeaponSync>();
            weaponSync.LIFHNEDIKHM = weaponScript;

            if (firePoint != null)
            {
                weaponSync.JDDPMKHOGEN = firePoint;
            }

            Renderer muzzle = tpsWeapon.GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer => renderer.name == "MuzzleFlash");
            if (muzzle)
            {
                weaponSync.NDGIAAEEGFO = muzzle;
            }

            tpsWeapon.SetActive(false);
        }
    }
}
