using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace NoRainDamage
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class NoRainDamage : BaseUnityPlugin
    {
        enum DamageFilter
        {
            ALL, INCLUSIVE, EXCLUSIVE
        }

        const string pluginGUID = "uk.co.oliapps.valheim.noraindamage";
        const string pluginName = "No Rain Damage";
        const string pluginVersion = "1.2.2";
        public static ManualLogSource logger;

        private static ConfigEntry<float> damagePerMinute;
        private static ConfigEntry<float> maxRainDamagePercentage;
        private static ConfigEntry<DamageFilter> damageFilter;
        private static ConfigEntry<string> damageFilterValues;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> wardRainProtection;

        private static Dictionary<int, float> minHealths;
        private static float minHealthPercentage;

        public void Awake()
        {
            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Set whether the mod is enabled");
            maxRainDamagePercentage = Config.Bind("Rain Damage", "Max Damage", 0.0f, 
                "Maximum damage rain can do to uncovered structures between 0.0 and 1.0");
            wardRainProtection = Config.Bind("Rain Damage", "Ward protects from rain", true, "Set whether an active Ward protects from rain and water damage");
            damagePerMinute = Config.Bind("Rain Damage", "Damage Per Minute", 5.0f, 
                "Damage per minute rain deals to uncovered structures");
            damageFilter = Config.Bind("Damage Filter", "Filter Type", DamageFilter.ALL, 
                "Set whether everything should be protected (ALL), certain things should be protected (INCLUSIVE) or certain things should not be protected (EXCLUSIVE)");
            damageFilterValues = Config.Bind("Damage Filter", "Filter List", "", 
                "If Filter type is INCLUSIVE or EXCLUSIVE, this list of comma-separated values will be used as a filter (will protect or damage if piece name contains value). Piece list included in mod download.");
            Config.Save();
            if (modEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(NoRainDamage), null);
                minHealths = new Dictionary<int, float>();
            }
        }

        [HarmonyPatch(typeof(WearNTear), "Awake")]
        [HarmonyPostfix]
        public static void WearNTear_Awake(ref WearNTear __instance)
        {
            minHealthPercentage = (1.0f - Mathf.Clamp(maxRainDamagePercentage.Value, 0.0f, 1.0f));
            minHealths.Add(__instance.GetInstanceID(), __instance.m_health * minHealthPercentage);
        }


        [HarmonyPatch(typeof(WearNTear), "UpdateWear")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool WearNTear_UpdateWear(ref WearNTear __instance)
        {
            bool OverrideWNT = OverrideWearNTear(ref __instance);
            bool wardProtection = wardRainProtection.Value;
            if (wardProtection || OverrideWNT)
            {
                ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(WearNTear), "m_nview").GetValue(__instance);
                if (m_nview == null || !m_nview.IsValid())
                    return false;
                bool shouldUpdate = (bool)AccessTools.Method(typeof(WearNTear), "ShouldUpdate").Invoke(__instance, new object[] { });
                if (m_nview.IsOwner() && shouldUpdate)
                {
                    if (ZNetScene.instance.OutsideActiveArea(__instance.transform.position))
                    {
                        float maxSupport = (float)AccessTools.Method(typeof(WearNTear), "GetMaxSupport").Invoke(__instance, new object[] { });
                        AccessTools.Field(typeof(WearNTear), "m_support").SetValue(__instance, maxSupport);
                        m_nview.GetZDO().Set("support", maxSupport);
                        return false;
                    }
                    float num = 0.0f;
                    bool haveRoof = (bool)AccessTools.Method(typeof(WearNTear), "HaveRoof").Invoke(__instance, new object[] { });
                    bool shouldDamage = EnvMan.instance.IsWet() && !haveRoof;
                    if ((bool)(UnityEngine.Object)__instance.m_wet)
                        __instance.m_wet.SetActive(shouldDamage);
                    if (__instance.m_noRoofWear && (double)__instance.GetHealthPercentage() > (OverrideWNT ? minHealthPercentage : 0.5f))
                    {
                        bool isUnderWater = (bool)AccessTools.Method(typeof(WearNTear), "IsUnderWater").Invoke(__instance, new object[] { });
                        float rainTimer = (float)AccessTools.Field(typeof(WearNTear), "m_rainTimer").GetValue(__instance);
                        float health = m_nview.GetZDO().GetFloat("health", __instance.m_health);
                        if (shouldDamage || isUnderWater)
                        {
                            if (wardProtection && PrivateArea.InsideFactionArea(__instance.transform.position, Character.Faction.Players))
                                rainTimer = 0.0f;
                            else if((double)rainTimer == 0.0)
                                rainTimer = Time.time;
                            else if ((double)Time.time - (double)rainTimer > 60.0)
                            {
                                rainTimer = Time.time;
                                num += (OverrideWNT ? damagePerMinute.Value : 5f);
                                if (IsTooMuchDamage(ref __instance, health, num))
                                {
                                    float reduceBy = HowMuchIsTooMuch(ref __instance, health, num);
                                    num -= reduceBy;
                                }
                            }
                        }
                        else
                        {
                            rainTimer = 0.0f;
                        }
                        AccessTools.Field(typeof(WearNTear), "m_rainTimer").SetValue(__instance, rainTimer);
                    }
                    if (__instance.m_noSupportWear)
                    {
                        AccessTools.Method(typeof(WearNTear), "UpdateSupport").Invoke(__instance, new object[] { });
                        bool haveSupport = (bool)AccessTools.Method(typeof(WearNTear), "HaveSupport").Invoke(__instance, new object[] { });
                        if (!haveSupport)
                        {
                            num = 100f;
                        }
                    }
                    bool canBeRemoved = (bool)AccessTools.Method(typeof(WearNTear), "CanBeRemoved").Invoke(__instance, new object[] { });
                    if ((double)num > 0.0 && !canBeRemoved)
                        num = 0.0f;
                    if ((double)num > 0.0)
                        __instance.ApplyDamage(num / 100f * __instance.m_health);
                }
                AccessTools.Method(typeof(WearNTear), "UpdateVisual").Invoke(__instance, new object[] { true });
                return false;
            } else
            {
                return true;
            }
        }

        private static bool OverrideWearNTear(ref WearNTear instance)
        {
            if (DamageFilter.ALL.Equals(damageFilter.Value)) return true;
            foreach (string name in damageFilterValues.Value.Split(',')) {
                if (instance.gameObject.name.Contains(name))
                {
                    switch (damageFilter.Value)
                    {
                        case DamageFilter.INCLUSIVE:
                            return true;
                        case DamageFilter.EXCLUSIVE:
                            return false;
                        default:
                            return true;
                    }
                }
            }
            return DamageFilter.EXCLUSIVE.Equals(damageFilter.Value);   
        }

        private static bool IsTooMuchDamage(ref WearNTear instance, float health, float damage)
        {
            float healthAfterDamage = health - (damage / 100f * instance.m_health);
            return healthAfterDamage < minHealths[instance.GetInstanceID()];
        }

        private static float HowMuchIsTooMuch(ref WearNTear instance, float health, float damage)
        {
            float remainingHealth = health - (damage / 100f * instance.m_health);
            float howMuchIsTooMuch = Mathf.Abs(minHealths[instance.GetInstanceID()] - remainingHealth);
            return howMuchIsTooMuch / instance.m_health * 100f;
        }

        private static void Log(string message)
        {
            logger.LogMessage(message);
        }

        [HarmonyPatch(typeof(WearNTear), "OnDestroy")]
        [HarmonyPostfix]
        public static void WearNTear_OnDestroy(ref WearNTear __instance)
        {
            minHealths.Remove(__instance.GetInstanceID());
        }
    }
}
