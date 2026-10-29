using Il2Cpp;
using System.Text.Json.Serialization;
using UnityEngine;

namespace CustomWeaponsFramework
{
    public enum AnimationType
    {
        OneHand,
        TwoHandRifle,
        TwoHandPistol
    }

    public class WeaponInfo
    {
        public string Id { get; set; } = "";
        [JsonIgnore]
        public string ContentHash { get; internal set; }

        public string Name { get; set; }
        [JsonIgnore]
        public string Path { get; set; }
        public WeaponScript.DOEEBEFKIJI Type { get; set; }
        public AnimationType AnimationType { get; set; }
        public int Fractions { get; set; }
        public int ProjectileSpeed { get; set; }
        public float WaitBeforeReload { get; set; }
        public float ShotDelay { get; set; }
        public float HitDelay { get; set; }
        public float FireRate { get; set; }
        public int BulletsPerClip { get; set; }
        public int Clips { get; set; }
        public float ReloadTime { get; set; }
        public bool SingleFire { get; set; }
        public float AimPositionX { get; set; }
        public float AimPositionY { get; set; }
        public float AimPositionZ { get; set; }
        public float Recoil { get; set; }
        public float AimRecoil { get; set; }
        public bool UseScope { get; set; }
        public int AimFov { get; set; }
        public bool AimAnimation { get; set; }
        public bool IsTool { get; set; }
        public bool CanAim { get; set; } = true;
        public int ShopPrice { get; set; }
        public bool ShopAvailable { get; set; }
        public bool SandboxAvailable { get; set; } = true;
        public string ShopInfo { get; set; }
        public float BulletLifeTime { get; set; } = 0;
        public int BulletDamage { get; set; } = 0;
        public int BulledSpeed { get; set; } = 0;
        public int BulletForce { get; set; } = 0;
        public bool BulletParticles { get; set; } = true;
        public float RecoilPower { get; set; }
        public float ShakePower { get; set; }
        public AudioClip ShotSound { get; set; }
        public AudioClip ReloadSound { get; set; }
        public Texture2D Icon { get; set; }
        public Texture2D Scope { get; set; }
        public Texture2D Crosshair { get; set; }
    }
}
