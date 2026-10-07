using System.Reflection;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("DroneRadiationSprayer")]
[assembly: AssemblyDescription("Standalone drone radiation-removal attachment; development candidate.")]
[assembly: AssemblyVersion("0.1.0.1")]
[assembly: AssemblyFileVersion("0.1.0.1")]

namespace Itachi.DroneRadiationSprayer
{
    public sealed class DroneRadiationSprayerMod : IModApi
    {
        public void InitMod(Mod mod)
        {
            SprayerController.Reset();
            new Harmony("itachi.droneradiationsprayer").PatchAll(Assembly.GetExecutingAssembly());
            Debug.Log("[DroneRadiationSprayer] v0.1.0.1 candidate loaded. Radius=25m; vanilla inhibitor=15s; pulse=1s.");
        }
    }

    [HarmonyPatch(typeof(EntityDrone), "OnUpdateEntity")]
    internal static class DroneUpdatePatch
    {
        // Observe the final vanilla shutdown/owner state. No original method is
        // skipped, and this patch never changes drone AI, health or inventory.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(EntityDrone __instance)
        {
            SprayerController.Tick(__instance);
        }
    }
}
