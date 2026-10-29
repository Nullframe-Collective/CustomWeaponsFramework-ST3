namespace CustomWeaponsFramework
{
    public static class WeaponCatalog
    {
        public static int ApiVersion => 1;

        public static bool Enabled { get; set; } = true;

        public static bool IsReady => Core.IsInitialized && Core.Error == null;

        public static IReadOnlyList<WeaponInfo> Items => Core.LoadedWeapons.AsReadOnly();

        public static event Action Ready;

        public static WeaponInfo Find(string id) =>
            Core.LoadedWeapons.FirstOrDefault(weapon => weapon.Id == id || weapon.Name == id);

        public static bool ValidId(string id) =>
            id is { Length: > 2 and <= 128 } &&
            id.Contains(':') &&
            id.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or ':' or '-' or '_' or '.');

        public static IDisposable Mount(string directory, string id)
        {
            WeaponInfo weapon = Core.ReadWeapon(directory, id);

            if (weapon.Id != id)
            {
                throw new InvalidDataException("The pack ID does not match the requested ID.");
            }

            WeaponInfo[] replaced = Core.LoadedWeapons.Where(item => item.Id == weapon.Id || item.Name == weapon.Name).ToArray();
            foreach (WeaponInfo item in replaced)
            {
                Core.LoadedWeapons.Remove(item);
                Core.Forget(item.Name);
            }

            Core.LoadedWeapons.Add(weapon);
            Core.LoadedWeapons.Sort((first, second) => StringComparer.Ordinal.Compare(first.Id, second.Id));
            return new Mounted(weapon, replaced);
        }

        public static void Refresh() => Core.Refresh();

        internal static void OnReady() => Ready?.Invoke();

        private sealed class Mounted(WeaponInfo weapon, WeaponInfo[] replaced) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;
                Core.LoadedWeapons.Remove(weapon);
                Core.Forget(weapon.Name);
                Core.LoadedWeapons.AddRange(replaced);
                Core.LoadedWeapons.Sort((first, second) => StringComparer.Ordinal.Compare(first.Id, second.Id));
            }
        }
    }
}
