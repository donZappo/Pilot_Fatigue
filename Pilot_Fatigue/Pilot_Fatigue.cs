using System;
using System.Reflection;
using BattleTech;
using Harmony;
using BattleTech.UI;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using HBS.Collections;
using HBS.Extensions;
using UnityEngine;


namespace Pilot_Fatigue
{
    public static class Pre_Control
    {
        public const string ModName = "Pilot_Fatigue";
        public const string ModId = "dZ.Zappo.Pilot_Fatigue";

        internal static ModSettings settings;
        internal static string ModDirectory;

        public static void Init(string directory, string modSettings)
        {
            ModDirectory = directory;
            try
            {
                settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
            }
            catch (Exception)
            {
                settings = new ModSettings();
            }

            var harmony = HarmonyInstance.Create(ModId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(SGBarracksRosterList), "SetSorting")]
        public static class SGBarracksRosterList_SetSorting_Patch
        {
            private static readonly HashSet<RectTransform> AdjustedIcons = new HashSet<RectTransform>();
            private const float SizeDeltaFactor = 2;
            private static readonly Vector2 AnchoredPositionOffset = new Vector2(6f, 35f);
            
            public static void Postfix(SGBarracksRosterList __instance, Dictionary<string, SGBarracksRosterSlot> ___currentRoster)
            {
                foreach (var pilot in ___currentRoster.Values)
                {
                    var timeoutIcon = pilot.GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(x => x.name == "mw_TimeOutIcon");
                    if (timeoutIcon == null)
                    {
                        return;
                    }

                    if (!AdjustedIcons.Contains(timeoutIcon))
                    {
                        if (pilot.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            AdjustedIcons.Add(timeoutIcon);
                            // mw_TimeOutIcon (SVGImporter.SVGImage)
                            timeoutIcon.sizeDelta /= SizeDeltaFactor;
                            timeoutIcon.anchoredPosition += AnchoredPositionOffset;
                        }
                    }
                    else
                    {
                        if (!pilot.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            AdjustedIcons.Remove(timeoutIcon);
                            timeoutIcon.sizeDelta *= SizeDeltaFactor;
                            timeoutIcon.anchoredPosition -= AnchoredPositionOffset;
                        }
                    }
                }

                __instance.ForceRefreshImmediate();
            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class Add_Fatigue_To_Pilots_Prefix
        {
            public static void Prefix(AAR_UnitStatusWidget __instance, SimGameState ___simState)
            {
                UnitResult unitResult = Traverse.Create(__instance).Field("UnitData").GetValue<UnitResult>();
                if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries == 0)
                {
                }
                else if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries > 0)
                {
                    unitResult.pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntry_MedBayHeal;
                    workOrderEntry_MedBayHeal = (WorkOrderEntry_MedBayHeal)___simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    ___simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                }

            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class Add_Fatigue_To_Pilots_Postfix
        {
            public static void Postfix(AAR_UnitStatusWidget __instance, SimGameState ___simState)
            {
                UnitResult unitResult = Traverse.Create(__instance).Field("UnitData").GetValue<UnitResult>();

                int FatigueTimeStart = settings.FatigueTimeStart;
                int GutsValue = unitResult.pilot.Guts;
                int TacticsValue = unitResult.pilot.Tactics;
                SimGameState simstate = Traverse.Create(__instance).Field("simState").GetValue<SimGameState>();
                int MoraleDiff = simstate.Morale - simstate.Constants.Story.StartingMorale;
                int MoraleModifier = 0;

                if (MoraleDiff <= settings.MoraleNegativeTierTwo)
                {
                    MoraleModifier = -2;
                }
                if (MoraleDiff <= settings.MoraleNegativeTierOne && MoraleDiff > settings.MoraleNegativeTierTwo)
                {
                    MoraleModifier = -1;
                }
                if (MoraleDiff < settings.MoralePositiveTierTwo && MoraleDiff >= settings.MoralePositiveTierOne)
                {
                    MoraleModifier = 1;
                }
                if (MoraleDiff >= settings.MoralePositiveTierTwo)
                {
                    MoraleModifier = 2;
                }

                //Reduction in Fatigue Time for Guts tiers.
                int GutsReduction = 0;
                if (GutsValue >= 4)
                    GutsReduction = 1;
                if (GutsValue >= 7)
                    GutsReduction = 2;
                else if (GutsValue == 10)
                    GutsReduction = 3;

                //Additional Fatigue Time for 'Mech damage.
                double MechDamage = (unitResult.mech.MechDefCurrentStructure + unitResult.mech.MechDefCurrentArmor) /
                    (unitResult.mech.MechDefAssignedArmor + unitResult.mech.MechDefMaxStructure);

                int MechDamageTime = (int)Math.Ceiling((1 - MechDamage) * settings.MechDamageMaxDays);

                //Calculate actual Fatigue Time for pilot.
                int FatigueTime = FatigueTimeStart + MechDamageTime - GutsReduction - MoraleModifier;

                if (unitResult.pilot.pilotDef.PilotTags.Contains("pilot_athletic") && settings.QuirksEnabled)
                    FatigueTime = (int)Math.Ceiling(FatigueTime/settings.pilot_athletic_FatigueDaysReductionFactor) - settings.pilot_athletic_FatigueDaysReduction;

                if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                    FatigueTime -= settings.pilot_athletic_FatigueDaysReduction;

                if (FatigueTime < settings.FatigueMinimum)
                {
                    FatigueTime = settings.FatigueMinimum;
                }

                if (settings.QuirksEnabled && unitResult.pilot.pilotDef.PilotTags.Contains("pilot_wealthy"))
                    FatigueTime += settings.pilot_wealthy_extra_fatigue;

                if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining == 0)
                {
                    unitResult.pilot.pilotDef.SetTimeoutTime(FatigueTime);
                    unitResult.pilot.pilotDef.PilotTags.Add("pilot_fatigued");
                }
                else if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining > 0)
                {
                    float roll = UnityEngine.Random.Range(1, 100);
                    float GutCheck = 5 * GutsValue;
                    if (settings.QuirksEnabled && (unitResult.pilot.pilotDef.PilotTags.Contains("pilot_gladiator") || unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green")))
                        GutCheck = GutCheck + 25;
                    if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                        GutCheck = GutCheck + 25;



                    int currenttime = unitResult.pilot.pilotDef.TimeoutRemaining;
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntry_MedBayHeal;
                    workOrderEntry_MedBayHeal = (WorkOrderEntry_MedBayHeal)___simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    ___simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                    int TotalFatigueTime = currenttime + FatigueTime;
                    if (TotalFatigueTime > settings.MaximumFatigueTime && !(settings.QuirksEnabled && unitResult.pilot.pilotDef.PilotTags.Contains("pilot_wealthy")))
                        TotalFatigueTime = settings.MaximumFatigueTime;
                    unitResult.pilot.pilotDef.SetTimeoutTime(TotalFatigueTime);
                    unitResult.pilot.pilotDef.PilotTags.Add("pilot_fatigued");

                    if (roll > GutCheck && (settings.LightInjuriesOn))
                    {
                        unitResult.pilot.pilotDef.PilotTags.Add("pilot_lightinjury");
                        unitResult.pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    }
                }
                if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                    unitResult.pilot.pilotDef.PilotTags.Remove("PQ_pilot_green");
            }
        }


        //[HarmonyPatch(typeof(SimGameState))]
        //[HarmonyPatch("GetInjuryCost")]
        //public static class GetInjuryCost_Postfix
        //{
        //    private static void Postfix(SimGameState __instance, Pilot p, ref int __result)
        //    {
        //        if (p.pilotDef.PilotTags.Contains("pilot_fatigued") | p.pilotDef.PilotTags.Contains("pilot_lightinjury") && p.pilotDef.TimeoutRemaining != 0)
        //        {
        //            __result = p.pilotDef.TimeoutRemaining;
        //        }
        //    }
        //}



        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("CanPilot", MethodType.Getter)]
        public static class BattleTech_Pilot_CanPilot_Prefix
        {
            public static void Postfix(Pilot __instance, ref bool __result)
            {
                if (__instance.Injuries == 0 && __instance.pilotDef.TimeoutRemaining > 0 && __instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                {
                    __result = true;
                }
            }
        }
        

        [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
        public static class CorrectTimeOut
        {
            public static void Postfix(SimGameState __instance, List<TemporarySimGameResult> ___TemporaryResultTracker)
            {
                List<Pilot> list = new List<Pilot>(__instance.PilotRoster);
                list.Add(__instance.Commander);
                for (int j = 0; j < list.Count; j++)
                {
                    Pilot pilot = list[j];
                    if (pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && pilot.Injuries == 0)
                    {
                        //pilot.StatCollection.ModifyStat<int>("Light Injury", 0, "Injuries", StatCollection.StatOperation.Set, 1, -1, true);
                    }
//                    if (pilot.pilotDef.TimeoutRemaining != 0)
//                    {
//                        int FatigueTime = pilot.pilotDef.TimeoutRemaining;
//                        pilot.pilotDef.SetTimeoutTime(FatigueTime - 1);
//                    }

                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    }

                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        pilot.pilotDef.PilotTags.Remove("pilot_lightinjury");
                        pilot.StatCollection.ModifyStat<int>("Light Injury Healed", 0, "Injuries", StatCollection.StatOperation.Set, 0, -1, true);
                    }
                    if (pilot.pilotDef.PilotTags.Contains("PF_pilot_morale_low"))
                    {
                        pilot.pilotDef.PilotTags.Remove("PF_pilot_morale_low");
                        pilot.pilotDef.PilotTags.Add("pilot_morale_low");

                        var eventTagSet = new TagSet();

                        Traverse.Create(eventTagSet).Field("items").SetValue(new string[] { "pilot_morale_low" });
                        Traverse.Create(eventTagSet).Field("tagSetSourceFile").SetValue("Tags/PilotTags");
                        Traverse.Create(eventTagSet).Method("UpdateHashCode").GetValue();

                        var EventTime = new TemporarySimGameResult();
                        EventTime.ResultDuration = settings.LowMoraleTime - 2;
                        EventTime.Scope = EventScope.MechWarrior;
                        EventTime.TemporaryResult = true;
                        EventTime.AddedTags = eventTagSet;
                        Traverse.Create(EventTime).Field("targetPilot").SetValue(pilot);

                        Traverse.Create(__instance).Method("AddOrRemoveTempTags", new[] { typeof(TemporarySimGameResult), typeof(bool) }).
                            GetValue(EventTime, true);
                        ___TemporaryResultTracker.Add(EventTime);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TaskManagementElement), "UpdateTaskInfo")]
        public static class Show_Fatigued_Info
        {
            public static void Postfix(TaskManagementElement __instance, TextMeshProUGUI ___subTitleText, UIColorRefTracker ___subTitleColor,
                WorkOrderEntry ___entry)
            {
                WorkOrderEntry_MedBayHeal healOrder = ___entry as WorkOrderEntry_MedBayHeal;
                try
                {
                    if (healOrder.Pilot.pilotDef.TimeoutRemaining > 0 && healOrder.Pilot.pilotDef.Injuries == 0
                        && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        ___subTitleText.text = "FATIGUED";
                        ___subTitleColor.SetUIColor(UIColor.Orange);
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    if (healOrder.Pilot.pilotDef.TimeoutRemaining > 0 && healOrder.Pilot.pilotDef.Injuries == 0
                        && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        ___subTitleText.text = "UNAVAILABLE";
                        ___subTitleColor.SetUIColor(UIColor.Blue);
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    if (healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        ___subTitleText.text = "LIGHT INJURY";
                        ___subTitleColor.SetUIColor(UIColor.Green);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        //Make Fatigue reduce resolve. 

        [HarmonyPatch(typeof(Team), "CollectUnitBaseline")]
        public static class Resolve_Reduction_Patch
        {
            public static void Postfix(Team __instance, ref int __result)
            {
                if (settings.FatigueReducesResolve == true)
                {
                    foreach (AbstractActor actor in __instance.units)
                    {
                        Pilot pilot = actor.GetPilot();
                        if (pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            int TimeOut = pilot.pilotDef.TimeoutRemaining;
                            int Penalty = 0;

                            if (pilot.pilotDef.PilotTags.Contains("pilot_gladiator") && settings.QuirksEnabled)
                            {
                                Penalty = (int)Math.Floor(TimeOut / settings.FatigueResolveFactor);
                            }
                            else
                            {
                                Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueResolveFactor);
                            }
                            __result = __result - Penalty;
                            if (!settings.AllowNegativeResolve && __result < 0)
                                __result = 0;
                        }
                    }
                }
            }
        }

        //Fatigue applies Low Spirits

        [HarmonyPatch(typeof(TurnEventNotification), "ShowTeamNotification")]
        public static class TurnEventNotification_Patch
        {
            public static void Prefix(TurnEventNotification __instance, Team team, bool ___hasBegunGame, 
                CombatGameState ___Combat)
            {
                if (settings.FatigueCausesLowSpirits)
                {
                    if (!___hasBegunGame && ___Combat.TurnDirector.CurrentRound <= 1)
                    {
                        foreach (AbstractActor actor in team.units)
                        {
                            Pilot pilot = actor.GetPilot();
                            if (pilot.pilotDef.PilotTags.Contains("pilot_fatigued") && !(settings.QuirksEnabled && pilot.pilotDef.PilotTags.Contains("pilot_gladiator")))
                            {
                                pilot.pilotDef.PilotTags.Add("pilot_morale_low");
                                pilot.pilotDef.PilotTags.Add("PF_pilot_morale_low");
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Gunnery", MethodType.Getter)]
        public class GunneryTimeModifier
        {
            public static void Postfix(Pilot __instance, ref int __result)
            {
                int Penalty = 0;
                int TimeOut = __instance.pilotDef.TimeoutRemaining;
                if (__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills)
                {
                    if (__instance.pilotDef.PilotTags.Contains("pilot_gladiator") && settings.QuirksEnabled)
                    {
                        Penalty = (int)Math.Floor(TimeOut / settings.FatigueFactor);
                    }
                    else
                    {
                        Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);
                    }
                }

                if (settings.InjuriesHurt)
                {
                    Penalty = Penalty + __instance.Injuries;
                }
                int NewValue = __result - Penalty;
                if (NewValue < 1)
                {
                    NewValue = 1;
                }
                __result = NewValue;
            }
        }
        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Piloting", MethodType.Getter)]
        public class PilotingHealthModifier
        {
            public static void Postfix(Pilot __instance, ref int __result)
            {
                int TimeOut = __instance.pilotDef.TimeoutRemaining;
                int Penalty = 0;
                if (__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills)
                    Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);

                if (settings.InjuriesHurt)
                {
                    Penalty = Penalty + __instance.Injuries;
                }
                int NewValue = __result - Penalty;

                if (NewValue < 1)
                {
                    NewValue = 1;
                }
                __result = NewValue;
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Tactics", MethodType.Getter)]
        public class TacticsHealthModifier
        {

            public static void Postfix(Pilot __instance, ref int __result)
            {
                int TimeOut = __instance.pilotDef.TimeoutRemaining;
                int Penalty = 0;
                if (__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills)
                    Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);

                if (settings.InjuriesHurt)
                {
                    Penalty = Penalty + __instance.Injuries;
                }
                int NewValue = __result - Penalty;
                if (NewValue < 1)
                {
                    NewValue = 1;
                }
                __result = NewValue;
            }
        }
        
        public static class Helper
        {
            //public static Settings LoadSettings()
            //{
            //    Settings result;
            //    try
            //    {
            //        using (StreamReader streamReader = new StreamReader("mods/Pilot_Fatigue/settings.json"))
            //        {
            //            result = JsonConvert.DeserializeObject<Settings>(streamReader.ReadToEnd());
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.LogError(ex);
            //        result = null;
            //    }
            //    return result;
            //}
            public class Logger
            {
                public static void LogError(Exception ex)
                {
                    using (StreamWriter streamWriter = new StreamWriter("mods/Pilot_Fatigue/Log.txt", true))
                    {
                        streamWriter.WriteLine(string.Concat(new string[]
                        {
                        "Message :",
                        ex.Message,
                        "<br/>",
                        Environment.NewLine,
                        "StackTrace :",
                        ex.StackTrace,
                        Environment.NewLine,
                        "Date :",
                        DateTime.Now.ToString()
                        }));
                        streamWriter.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
                    }
                }

                public static void LogLine(string line)
                {
                    string path = "mods/Pilot_Fatigue/Log.txt";
                    using (StreamWriter streamWriter = new StreamWriter(path, true))
                    {
                        streamWriter.WriteLine(line + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                        streamWriter.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
                    }
                }
            }
        }
        internal class ModSettings
        {
            public int FatigueTimeStart = 7;
            public int MoraleModifier = 5;
            public int FatigueMinimum = 0;
            public int MoralePositiveTierOne = 5;
            public int MoralePositiveTierTwo = 15;
            public int MoraleNegativeTierOne = -5;
            public int MoraleNegativeTierTwo = -15;
            public double FatigueFactor = 2.5;
            public bool InjuriesHurt = true;
            public int pilot_athletic_FatigueDaysReduction = 1;
            public double pilot_athletic_FatigueDaysReductionFactor = 0.5;
            public bool QuirksEnabled = false;
            public bool FatigueReducesResolve = true;
            public bool FatigueReducesSkills = false;
            public double FatigueResolveFactor = 2.5;
            public bool FatigueCausesLowSpirits = true;
            public int LowMoraleTime = 14;
            public bool LightInjuriesOn = true;
            public int MaximumFatigueTime = 14;
            public bool AllowNegativeResolve = false;
            public int pilot_wealthy_extra_fatigue = 1;
            public int MechDamageMaxDays = 5;
        }
    }
}