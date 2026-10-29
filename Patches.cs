using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CustomWeaponsFramework
{
    [HarmonyPatch(typeof(WeaponScript), "Update")]
    public static class ToolWeaponPatch
    {
        public static bool Prefix(WeaponScript __instance)
        {
            return !WeaponInventory.IsTool(__instance.JIFANOLAADI);
        }
    }

    [HarmonyPatch(typeof(Volume), "Awake")]
    public static class VolumePatch
    {
        public static void Postfix(Volume __instance)
        {
            foreach (WeaponInfo weapon in Core.LoadedWeapons)
            {
                if (!WeaponCatalog.Enabled || weapon == null || !weapon.SandboxAvailable) continue;

                Add(__instance.PDKPIOHFCCK[4].options, weapon.Name);
                Add(__instance.PDKPIOHFCCK[5].options, weapon.Name);
            }
        }

        private static void Add(Il2CppSystem.Collections.Generic.List<Volume.option> options, string name)
        {
            foreach (Volume.option option in options)
            {
                if (option != null && option.optionName == name) return;
            }

            options.Add(new Volume.option()
            {
                optionName = name
            });
        }
    }

    [HarmonyPatch(typeof(ShopSystem), "Awake")]
    public static class ShopSystemAwake
    {
        public static void Postfix(ShopSystem __instance)
        {
            foreach (WeaponInfo weapon in Core.LoadedWeapons)
            {
                if (!WeaponCatalog.Enabled || weapon == null || !weapon.ShopAvailable || Listed(__instance, weapon.Name)) continue;

                ShopSystem.weapon shopWeapon = new()
                {
                    name = weapon.Name,
                    price = weapon.ShopPrice,
                    bulletPrice = weapon.BulletDamage / 2,
                    type = weapon.ShopInfo
                };

                __instance.JDCKBMLGDJD.Add(shopWeapon);
            }
        }

        private static bool Listed(ShopSystem shop, string name)
        {
            foreach (ShopSystem.weapon item in shop.JDCKBMLGDJD)
            {
                if (item != null && item.name == name) return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerNetworkController), "Awake")]
    public static class OnPlayerSpawned
    {
        public static void Postfix(PlayerNetworkController __instance)
        {
            MelonCoroutines.Start(Core.CreateFirstPersonWeapons(__instance.gameObject));
        }
    }

    [HarmonyPatch(typeof(LadderPlayer), "Start")]
    public static class OnOfflinePlayerSpawned
    {
        public static void Postfix(LadderPlayer __instance)
        {
            if (__instance.GetComponentInParent<PlayerNetworkController>()) return;

            MelonCoroutines.Start(Core.CreateFirstPersonWeapons(__instance.gameObject));
        }
    }
}
