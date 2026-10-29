using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CustomWeaponsFramework
{
    internal sealed class WeaponReferences
    {
        internal readonly WeaponManager Manager;
        internal readonly CharacterAnimation Animation;
        internal readonly SkinnedMeshRenderer Arm;
        internal readonly GameObject[] Templates;

        internal WeaponReferences(GameObject player, PlayerNetworkController controller)
        {
            Manager = player.GetComponentInChildren(Il2CppType.Of<WeaponManager>(), true)?.Cast<WeaponManager>();
            Animation = controller && controller.ODDPEBHLOBE ? controller.ODDPEBHLOBE :
                player.GetComponentInChildren<CharacterAnimation>(true);

            SyncCustomization customization = player.GetComponentsInChildren<SyncCustomization>(true)
                .FirstOrDefault(value => !value.IGDAIDNEJBO && value.DCBPBGEFBHD);
            Arm = customization ? customization.DCBPBGEFBHD : null;

            WeaponScript[] weapons = Manager ? Manager.GetComponentsInChildren<WeaponScript>(true).ToArray() : Array.Empty<WeaponScript>();

            string[] ids = ["AKM Rifle", "Grenade Launcher", "MCS870", "Knife"];
            Templates = ids.Select((id, index) => weapons.FirstOrDefault(weapon =>
                weapon.JIFANOLAADI == id && (int)weapon.PFGPKNNMAHG == index)?.gameObject).ToArray();
        }

        internal static Transform FirePoint(PlayerNetworkController player) =>
            player && player.HDCJMMEMDEJ ? player.HDCJMMEMDEJ.GetComponentsInChildren<WeaponSync>(true)
                .FirstOrDefault(sync => sync.JDDPMKHOGEN)?.JDDPMKHOGEN : null;
    }
}
