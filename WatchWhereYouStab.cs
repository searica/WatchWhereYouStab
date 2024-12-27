// Ignore Spelling: CameraTweaks Jotunn

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Logging;

namespace VerticalAttacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    internal sealed class WatchWhereYouStab : BaseUnityPlugin
    {
        internal const string Author = "Searica";
        public const string PluginName = "WatchWhereYouStab";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "0.1.2";
        
        public static WatchWhereYouStab Instance;
        private const string MainSection = "Global";
        internal ConfigEntry<float> MaxAngle;       

        public void Awake()
        {
            Instance = this;
            Log.Init(Logger);

            Config.Init(PluginGUID, false);

            Log.Verbosity = Config.BindConfigInOrder(
                MainSection,
                "Verbosity",
                Log.LogLevel.Low,
                "Low will log basic information about the mod. Medium will log information that " +
                "is useful for troubleshooting. High will log a lot of information, do not set " +
                "it to this without good reason as it will slow Down your game.",
                synced: false
            );

            MaxAngle = Config.BindConfigInOrder(
                MainSection,
                "Max Attack Angle",
                65f,
                "Angle you can aim melee attacks up or down by. This is only applied to attacks that currently have a smaller maximum angle than the configured value.",
                new AcceptableValueRange<float>(5f, 90f),
                synced: false
            );

            Config.Save();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);
            Game.isModded = true;

            Config.SetupWatcher();
        }

        public void OnDestroy()
        {
            Config.Save();
        }
    }


    [HarmonyPatch]
    internal static class AttackPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch((typeof(Attack)), nameof(Attack.DoMeleeAttack))]
        private static void DoMeleeAttackPrefix(ref Attack __instance)
        {
            if (!Player.m_localPlayer ||
                !Player.m_localPlayer.TryGetComponent(out Character character) || 
                __instance.m_character != character)
            {

                return;
            }

            // Sets max angle limit for RotateTowards method
            __instance.m_maxYAngle = Mathf.Max(__instance.m_maxYAngle, WatchWhereYouStab.Instance.MaxAngle.Value);
        }
    }
}