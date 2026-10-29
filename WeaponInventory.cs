using Il2Cpp;
using UnityEngine;

namespace CustomWeaponsFramework
{
    public static class WeaponInventory
    {
        private static string _previous;
        private static string _selected;
        private static WeaponManager _manager;
        private static readonly Dictionary<string, WeaponScript> Weapons = new(StringComparer.Ordinal);
        private static readonly Dictionary<IntPtr, GameObject> Models = new();
        public static event Action Ready;
        public static event Action<string> SelectedChanged;

        public static WeaponManager Manager => Core.WeaponRoot && _manager ? _manager : null;

        public static bool IsReady => WeaponCatalog.Enabled && WeaponCatalog.IsReady && Core.AssetsReady && Core.HasWeapons();

        public static string Selected => Manager && Manager.FGGENNCGLJI ? Manager.FGGENNCGLJI.JIFANOLAADI : null;

        public static string SelectedId => WeaponCatalog.Find(Selected)?.Id;

        public static bool IsTool(string id) => WeaponCatalog.Find(id)?.IsTool == true;

        internal static void Bind(WeaponManager manager)
        {
            if (_manager && manager && _manager.Pointer == manager.Pointer) return;

            Reset();
            _manager = manager;

            if (!manager) return;

            foreach (WeaponScript script in manager.GetComponentsInChildren<WeaponScript>(true))
            {
                Register(script);
            }
        }

        internal static void Register(WeaponScript script, GameObject model = null)
        {
            if (!script) return;

            if (!string.IsNullOrEmpty(script.JIFANOLAADI))
            {
                Weapons[script.JIFANOLAADI] = script;
            }

            Weapons[script.name] = script;

            if (model)
            {
                Models[script.Pointer] = model;
            }
        }

        internal static WeaponScript Find(string name) =>
            name != null && Weapons.TryGetValue(name, out WeaponScript script) && script ? script : null;

        internal static void Reset()
        {
            _manager = null;
            _previous = null;
            Weapons.Clear();
            Models.Clear();
        }

        public static bool Grant(string id)
        {
            if (!WeaponCatalog.Enabled) return false;

            WeaponManager manager = Manager;
            string name = WeaponCatalog.Find(id)?.Name ?? id;
            WeaponScript script = manager ? Find(name) : null;
            if (!script) return false;

            if (!manager.DHFOMKJHLBM.Contains(script))
            {
                manager.DHFOMKJHLBM.Add(script);
            }

            return true;
        }

        public static bool Select(string id)
        {
            string name = WeaponCatalog.Find(id)?.Name ?? id;
            if (!Grant(id)) return false;

            WeaponManager manager = Manager;
            if (Selected == name) return true;
            if (!manager.FGGENNCGLJI) return false;

            WeaponScript previous = manager.FGGENNCGLJI;
            WeaponScript script = Find(name);

            if (!IsTool(Selected))
            {
                _previous = Selected;
            }

            manager.GKMCALGHDFK = manager.DHFOMKJHLBM.IndexOf(script);
            manager.FGGENNCGLJI = script;
            manager.StartCoroutine(manager.JNCHFDANPPC(previous.gameObject, script.gameObject));

            return true;
        }

        public static bool Holster()
        {
            if (!IsTool(Selected)) return true;

            WeaponManager manager = Manager;
            if (!manager) return false;

            string name = _previous;
            if (!Find(name))
            {
                name = manager.DHFOMKJHLBM.ToArray().FirstOrDefault(weapon => weapon && !IsTool(weapon.JIFANOLAADI))?.JIFANOLAADI;
            }

            return name != null && Select(name);
        }

        public static GameObject WorldModel(string id, Transform parent)
        {
            string name = WeaponCatalog.Find(id)?.Name ?? id;
            if (!parent || name == null || !Core.LoadedBundles.TryGetValue(name, out var bundle) || bundle is not { Length: >= 2 } || !bundle[1])
            {
                return null;
            }

            GameObject model = GameObject.Instantiate(bundle[1], parent, false);
            model.name = name;

            return model;
        }

        public static GameObject ViewModel(string id)
        {
            string name = WeaponCatalog.Find(id)?.Name ?? id;
            WeaponScript weapon = Manager ? Find(name) : null;

            return weapon && Models.TryGetValue(weapon.Pointer, out GameObject model) && model ? model : null;
        }

        internal static void Update()
        {
            string selected = Selected;
            if (_selected == selected) return;

            _selected = selected;
            SelectedChanged?.Invoke(selected);
        }

        internal static void OnReady() => Ready?.Invoke();
    }
}
