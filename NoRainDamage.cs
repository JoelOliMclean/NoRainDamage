using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoRainDamage
{
    [BepInPlugin("uk.co.oliapps.valheim.noraindamage", "No Rain Damage", "1.0.0")]
    public class NoRainDamage : BaseUnityPlugin
    {
        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(NoRainDamage), null);
        }

        [HarmonyPatch(typeof(WearNTear), "HaveRoof")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void HaveRoof(ref bool __result)
        {
            __result = true;
        }
    }
}