using System;
using System.Reflection;
using BattleTech;
using Harmony;
using BattleTech.UI;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using TMPro;


namespace Pilot_Fatigue
{
    public static class PreControl
    {
        public const string ModName = "Pilot_Fatigue";
        public const string ModId = "dZ.Zappo.Pilot_Fatigue";

        internal static ModSettings Settings;
        internal static string ModDirectory;

        public static void Init(string directory, string modSettings)
        {
            ModDirectory = directory;
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }

            var harmony = HarmonyInstance.Create(ModId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AddFatigueToPilotsPrefix
        {
            public static void Prefix(AAR_UnitStatusWidget instance, SimGameState simState)
            {
                UnitResult unitResult = Traverse.Create(instance).Field("UnitData").GetValue<UnitResult>();
                if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries == 0)
                {
                }
                else if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries > 0)
                {
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntryMedBayHeal;
                    workOrderEntryMedBayHeal = (WorkOrderEntry_MedBayHeal)simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                }

            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AddFatigueToPilotsPostfix
        {
            public static void Postfix(AAR_UnitStatusWidget instance, SimGameState simState)
            {
                UnitResult unitResult = Traverse.Create(instance).Field("UnitData").GetValue<UnitResult>();

                int fatigueTimeStart = Settings.FatigueTimeStart;
                int gutsValue = unitResult.pilot.Guts;
                SimGameState simstate = Traverse.Create(instance).Field("simState").GetValue<SimGameState>();
                int currentMorale = simstate.Morale;
                int moraleDiff = currentMorale - simstate.Morale;
                int moraleModifier = 0;

                if (moraleDiff <= Settings.MoraleNegativeTierTwo)
                {
                    moraleModifier = -2;
                }
                if (moraleDiff <= Settings.MoraleNegativeTierOne && moraleDiff > Settings.MoraleNegativeTierTwo)
                {
                    moraleModifier = -1;
                }
                if (moraleDiff < Settings.MoralePositiveTierTwo && moraleDiff >= Settings.MoralePositiveTierOne)
                {
                    moraleModifier = 1;
                }
                if (moraleDiff >= Settings.MoralePositiveTierTwo)
                {
                    moraleModifier = 2;
                }

                int fatigueTime = 1 + fatigueTimeStart - gutsValue / 2 - moraleModifier;

                if (unitResult.pilot.pilotDef.PilotTags.Contains("pilot_athletic") && Settings.QuirksEnabled)
                    fatigueTime = fatigueTime - Settings.PilotAthleticFatigueDaysReduction;

                if (fatigueTime <= (Settings.FatigueMinimum + 1))
                {
                    fatigueTime = Settings.FatigueMinimum + 1;
                }

                if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining == 0)
                {
                    unitResult.pilot.pilotDef.SetTimeoutTime(fatigueTime);
                    unitResult.pilot.pilotDef.PilotTags.Add("pilot_fatigued");
                }
                else if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining > 0)
                {
                    float roll = UnityEngine.Random.Range(1, 100);
                    float gutCheck = 10 * gutsValue;
                    int currentTime = unitResult.pilot.pilotDef.TimeoutRemaining;
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntryMedBayHeal;
                    workOrderEntryMedBayHeal = (WorkOrderEntry_MedBayHeal)simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                    unitResult.pilot.pilotDef.SetTimeoutTime(currentTime + fatigueTime);

                    unitResult.pilot.pilotDef.PilotTags.Add(roll > gutCheck ? "pilot_lightinjury" : "pilot_fatigued");
                }
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("CanPilot", PropertyMethod.Getter)]
        public static class BattleTechPilotCanPilotPrefix
        {
            private static void Postfix(Pilot instance, ref bool result)
            {
                if (instance.Injuries == 0 && instance.pilotDef.TimeoutRemaining > 0 && instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                {
                    result = true;
                }
            }
        }
        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Gunnery", PropertyMethod.Getter)]
        public class GunneryTimeModifier
        {
            public static void Postfix(Pilot instance, ref int result)
            {
                int penalty = 0;
                int timeOut = instance.pilotDef.TimeoutRemaining;
                if (instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                {
                    if (instance.pilotDef.PilotTags.Contains("pilot_gladiator") && Settings.QuirksEnabled)
                    {
                        penalty = (int)Math.Floor(timeOut / Settings.FatigueFactor);
                    }
                    else
                    {
                        penalty = (int)Math.Ceiling(timeOut / Settings.FatigueFactor);
                    }
                }

                if (Settings.InjuriesHurt)
                {
                    penalty = penalty + instance.Injuries;
                }
                int newValue = result - penalty;
                if (newValue < 1)
                {
                    newValue = 1;
                }
                result = newValue;
            }
        }
        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Piloting", PropertyMethod.Getter)]
        public class PilotingHealthModifier
        {
            public static void Postfix(Pilot instance, ref int result)
            {
                int timeOut = instance.pilotDef.TimeoutRemaining;
                int penalty = 0;
                if (instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    penalty = (int)Math.Ceiling(timeOut / Settings.FatigueFactor);

                if (Settings.InjuriesHurt)
                {
                    penalty = penalty + instance.Injuries;
                }
                int newValue = result - penalty;

                if (newValue < 1)
                {
                    newValue = 1;
                }
                result = newValue;
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Tactics", PropertyMethod.Getter)]
        public class TacticsHealthModifier
        {

            public static void Postfix(Pilot instance, ref int result)
            {
                int timeOut = instance.pilotDef.TimeoutRemaining;
                int penalty = 0;
                if (instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    penalty = (int)Math.Ceiling(timeOut / Settings.FatigueFactor);

                if (Settings.InjuriesHurt)
                {
                    penalty = penalty + instance.Injuries;
                }
                int newValue = result - penalty;
                if (newValue < 1)
                {
                    newValue = 1;
                }
                result = newValue;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
        public static class CorrectTimeOut
        {
            public static void Postfix(SimGameState instance)
            {
                List<Pilot> list = new List<Pilot>(instance.PilotRoster);
                list.Add(instance.Commander);
                for (int j = 0; j < list.Count; j++)
                {
                    Pilot pilot = list[j];
                    if (pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && pilot.Injuries == 0)
                    {
                        pilot.StatCollection.ModifyStat<int>("Light Injury", 0, "Injuries", StatCollection.StatOperation.Set, 1, -1, true);
                    }
                    if (pilot.pilotDef.TimeoutRemaining != 0)
                    {
                        int fatigueTime = pilot.pilotDef.TimeoutRemaining;
                        pilot.pilotDef.SetTimeoutTime(fatigueTime - 1);
                    }

                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        pilot.pilotDef.PilotTags.Remove("pilot_fatigued");

                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        pilot.pilotDef.PilotTags.Remove("pilot_lightinjury");
                        pilot.StatCollection.ModifyStat<int>("Light Injury Healed", 0, "Injuries", StatCollection.StatOperation.Set, 0, -1, true);
                    }

                }
            }
        }

        [HarmonyPatch(typeof(TaskManagementElement), "UpdateTaskInfo")]
        public static class ShowFatiguedInfo
        {
            public static void Postfix(TaskManagementElement instance, TextMeshProUGUI subTitleText, UIColorRefTracker subTitleColor,
                WorkOrderEntry entry)
            {
                WorkOrderEntry_MedBayHeal healOrder = entry as WorkOrderEntry_MedBayHeal;
                try
                {
                    if (healOrder.Pilot.pilotDef.TimeoutRemaining > 0 && healOrder.Pilot.pilotDef.Injuries == 0
                        && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        subTitleText.text = "FATIGUED";
                        subTitleColor.SetUIColor(UIColor.Orange);
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
                        subTitleText.text = "UNAVAILABLE";
                        subTitleColor.SetUIColor(UIColor.Blue);
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    if (healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        subTitleText.text = "LIGHT INJURY";
                        subTitleColor.SetUIColor(UIColor.Green);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public static class Helper
        {
            public static Settings LoadSettings()
            {
                Settings result;
                try
                {
                    using (StreamReader streamReader = new StreamReader(string.Format("{0}/settings.json", ModDirectory)))
                    {
                        result = JsonConvert.DeserializeObject<Settings>(streamReader.ReadToEnd());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    result = null;
                }
                return result;
            }
            public class Logger
            {
                public static void LogError(Exception ex)
                {
                    using (StreamWriter streamWriter = new StreamWriter(string.Format("{0}/Log.txt", ModDirectory), true))
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
                    string path = string.Format("{0}/Log.txt", ModDirectory);
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
            public int StartingMorale = 25;
            public int FatigueMinimum = 0;
            public int MoralePositiveTierOne = 5;
            public int MoralePositiveTierTwo = 15;
            public int MoraleNegativeTierOne = -5;
            public int MoraleNegativeTierTwo = -15;
            public double FatigueFactor = 2.5;
            public bool InjuriesHurt = true;
            public int PilotAthleticFatigueDaysReduction = 1;
            public bool QuirksEnabled = false;
        }
    }
}