using UnityEngine;
using Verse;

namespace CleanKitchen
{
    public class Settings : ModSettings
    {
        public static bool adv_cleaning = true;
        public static int adv_clean_num = 0;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            Rect viewRect = new Rect(0f, 0f, inRect.width, 36f*26f);
            viewRect.xMax *= 0.9f;

            listing_Standard.Begin(viewRect);
            GUI.EndGroup();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref adv_cleaning, "adv_cleaning");
            Scribe_Values.Look(ref adv_clean_num, "adv_clean_num");
        }
    }
}
