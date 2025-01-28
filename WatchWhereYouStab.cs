// Ignore Spelling: CameraTweaks Jotunn

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Logging;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System;

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
        internal ConfigEntry<bool> FixedHeight;
        internal ConfigEntry<float> AngleCorrection;

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

            FixedHeight = Config.BindConfigInOrder(
                MainSection,
                "Fixed Attack Height",
                false,
                "Different melee attacks originate from different positions based on their vanilla animations. Enable this to make all melee attacks " +
                "originate from the same position to make aiming more consistent.",
                synced: false
            );

            AngleCorrection = Config.BindConfigInOrder(
                MainSection,
                "Angle Correction",
                0f,
                "Angle to apply as a correction to your current look direction. This can be used to offset the way the camera is usually pointing downwards. " +
                "If positive then attacks will be aimed above the current camera direction, if negative they will be aimed below.",
                new AcceptableValueRange<float>(-30f, 30f),
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


            if (WatchWhereYouStab.Instance.FixedHeight.Value)
            {
                __instance.m_attackHeight = 1f;
            }
        }

        /// <summary>
        ///     Modify calcuation of the melee attack direction by replacing the call to Attack.GetMeleeAttackDir
        ///     with a call to AttachPatch.GetMeleeAttackDirection.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler]
        [HarmonyPatch((typeof(Attack)), nameof(Attack.DoMeleeAttack))]
        private static IEnumerable<CodeInstruction> DoMeleeAttack_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatch[] targetCodeMatches =
            [
                new(OpCodes.Call, AccessTools.Method(typeof(Attack), nameof(Attack.GetMeleeAttackDir)))
            ];

            CodeInstruction replacementCode = new(
                OpCodes.Call,
                AccessTools.Method(
                    typeof(AttackPatch),
                    nameof(GetMeleeAttackDirection)
                )
            );


            return new CodeMatcher(instructions)
                .Start()
                .MatchStartForward(targetCodeMatches)
                .ThrowIfNotMatch("Failed to match code in Attack.DoMeleeAttack!")
                .SetInstructionAndAdvance(replacementCode)
                .ThrowIfInvalid("Failed to patch Attack.DoMeleeAttack!")
                .InstructionEnumeration();
        }

        //[Error: Unity Log] InvalidProgramException: Invalid IL code in (wrapper dynamic-method) Attack:DMD<Attack::DoMeleeAttack>(Attack): IL_0018: call      0x00000005
        
        /// <summary>
        ///     Apply the angle correction if needed.
        /// </summary>
        /// <param name="attack"></param>
        /// <param name="originJoint"></param>
        /// <param name="attackDir"></param>
        private static void GetMeleeAttackDirection(Attack attack, out Transform originJoint, out Vector3 attackDir)
        {
            originJoint = attack.GetAttackOrigin();
            Vector3 forward = attack.m_character.transform.forward;
            Vector3 aimDir = attack.m_character.GetAimDir(originJoint.position);
            aimDir.x = forward.x;
            aimDir.z = forward.z;
            aimDir.Normalize();

            float angleCorrection = WatchWhereYouStab.Instance.AngleCorrection.Value;
            if (angleCorrection != 0f) 
            {
                Vector3 targetDir = Mathf.Sign(angleCorrection) * Vector3.up;
                aimDir = Vector3.RotateTowards(aimDir, targetDir, Mathf.Deg2Rad * Mathf.Abs(angleCorrection), 0.0f);
            }
                        
            attackDir = Vector3.RotateTowards(attack.m_character.transform.forward, aimDir, Mathf.Deg2Rad * attack.m_maxYAngle, 10f);
            Log.LogInfo("Doing the thing!");
        }
    }
}