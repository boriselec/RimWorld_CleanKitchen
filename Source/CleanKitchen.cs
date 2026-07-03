using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;

namespace CleanKitchen
{
    [StaticConstructorOnStartup]
    public class CleanKitchen : Mod
    {
        public static Settings Settings;
        public CleanKitchen(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.boriselec.rimworld.mod.CleanKitchen");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.GetSettings<Settings>();
        }
        
        public void Save()
        {
            LoadedModManager.GetMod<CleanKitchen>().GetSettings<Settings>().Write();
        }

        public override string SettingsCategory()
        {
            return "CleanKitchen";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }
}
