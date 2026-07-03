using HarmonyLib;
using System.Reflection;
using Verse;

namespace CleanKitchen
{
    [StaticConstructorOnStartup]
    public class CleanKitchen : Mod
    {
        public CleanKitchen(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.boriselec.rimworld.mod.CleanKitchen");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
