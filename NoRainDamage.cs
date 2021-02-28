using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoRainDamage
{
    [BepInPlugin("uk.co.oliapps.valheim.noraindamage", "No Rain Damage", "0.0.1")]
    public class NoRainDamage
    {
        private static NoRainDamage INSTANCE;

        public NoRainDamage instance
        {
            get
            {
                return NoRainDamage.INSTANCE;
            }
        }

        public void Awake()
        {
            NoRainDamage.INSTANCE = this;
            Harmony.CreateAndPatchAll(typeof(NoRainDamage), null);
        }

        [HarmonyPatch(typeof(WearNTear), "HaveRoof")]
        [HarmonyPrefix]
        public static bool HaveRoof(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
