using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace PrisonerRansom
{
    public class RansomSettings : ModSettings
    {
        public float ransomFactor=2f;
        public float ransomGoodwill=5f;
        public float ransomGoodwillFail=-10f;
        public float ransomFailChance=20f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.ransomFactor, "ransomFactor", 2f);
            Scribe_Values.Look(ref this.ransomGoodwill, "ransomGoodwill", 5f);
            Scribe_Values.Look(ref this.ransomGoodwillFail, "ransomGoodWillFail", -10f);
            Scribe_Values.Look(ref this.ransomFailChance, "ransomFailChance", 20f);
        }
    }

    class PrisonerRansom : Mod
    {
        RansomSettings settings;

        public PrisonerRansom(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<RansomSettings>();
            ReplacementCode.settings = this.settings;
        }

        public override string SettingsCategory() => "Prisoner Ransom";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            this.settings.ransomFactor = Widgets.HorizontalSlider(inRect.TopHalf().TopHalf(), this.settings.ransomFactor, -5f, 5f, true, "Ransom amount factor: " + this.settings.ransomFactor + "\nDetermines the factor that the value of a prisoner is multiplied with", "5", "5");
            this.settings.ransomGoodwill = Widgets.HorizontalSlider(inRect.TopHalf().BottomHalf(), this.settings.ransomGoodwill, -50f, 50f, true, "Goodwill effect on success: " + this.settings.ransomGoodwill + "\nDetermines the value the relationship get's affected with on success", "-50", "50");
            this.settings.ransomGoodwillFail = Widgets.HorizontalSlider(inRect.BottomHalf().TopHalf(), this.settings.ransomGoodwillFail, -50f, 50f, true, "Goodwill effect on failure: " + this.settings.ransomGoodwillFail + "\nDetermines the value the relationship get's affected with on failure", "-50", "50");
            this.settings.ransomFailChance = Widgets.HorizontalSlider(inRect.BottomHalf().BottomHalf().TopHalf(), this.settings.ransomFailChance, 0f, 100f, true, "Chance of failure: " + this.settings.ransomFailChance + "\nDetermines the probability of a ransom failing", "0%", "100%");

            this.settings.Write();
        }
    }

    [StaticConstructorOnStartup]
    public static class ReplacementCode
    {
        public static RansomSettings settings;

        static ReplacementCode()
        {
            Harmony harmony = new Harmony("rimworld.anarcraft.prisoner_ransom");
            harmony.Patch(typeof(FactionDialogMaker).GetMethod("FactionDialogFor"), null, new HarmonyMethod(typeof(ReplacementCode), nameof(FactionDialogForPostFix)));
        }
        
        public static void FactionDialogForPostFix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (faction.HostileTo(Faction.OfPlayer))
            {
                __result.options.Insert(0, RansomPrisoner(faction, negotiator, negotiator.Map));
            }
        }

        private static DiaOption RansomPrisoner(Faction faction, Pawn negotiator, Map map)
        {
            IEnumerable<Pawn> prisoners = (from p in map.mapPawns.PrisonersOfColony where p.Faction == faction select p);
            //numberOfPrisoners = prisoners.Count();
            DiaOption dia = new DiaOption("Demand ransom for prisoner | " + prisoners.Count() + " imprisoned");
            if (prisoners.Count() <= 0)
                dia.Disable("Try capturing someone");
            DiaNode diaNode = new DiaNode("Prisoner(s) of " + faction + ":");
            foreach (Pawn p in prisoners)
            {
                int value = UnityEngine.Mathf.RoundToInt(p.MarketValue * (faction.leader==p?4:settings.ransomFactor));
                DiaOption diaOption = new DiaOption(p.Name.ToStringFull + " (" + value + ")")
                {
                    action = delegate
                    {
                        if (UnityEngine.Random.value + negotiator.skills.GetSkill(SkillDefOf.Social).Level / 50 - 0.2 > (settings.ransomFailChance / 100f))
                        {
                            Messages.Message(faction + " delivered the ransom of " + value + " silver.", MessageTypeDefOf.PositiveEvent);
                            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                            silver.stackCount = value;
                            TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, silver);
                            if (p.Spawned)
                            {
                                GenGuest.PrisonerRelease(p);
                                p.DeSpawn();
                            }
                        //TaleRecorder.RecordTale(TaleDefOf.SoldPrisoner);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, (int)(faction.leader == p ? 50 : settings.ransomGoodwill));
                            Messages.Message((faction.leader == p ? "You sent the leader of " + faction + ", " : "You sent prisoner ") + p + " home. (+" + (faction.leader == p ? 50 : settings.ransomGoodwill) + " Goodwill)", MessageTypeDefOf.PositiveEvent);
                        }
                        else
                        {
                            Messages.Message(faction + " did not accept the ransom!", MessageTypeDefOf.NegativeEvent);
                            faction.TryAffectGoodwillWith(Faction.OfPlayer, (int)(faction.leader == p ? -50 : settings.ransomGoodwillFail));
                            IncidentParms incidentParms = new IncidentParms()
                            {
                                faction = faction,
                                points = (float)Rand.Range(value / 3, value / 2),
                                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                                target = map
                            };
                            IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms);
                        }
                    }
                };
                diaNode.options.Add(diaOption);
                diaOption.resolveTree = true;
            }
            DiaOption diaOption2 = new DiaOption("(" + "Disconnect".Translate() + ")");
            diaNode.options.Add(diaOption2);
            diaOption2.resolveTree = true;
            dia.link = diaNode;
            return dia;
        }
    }
}