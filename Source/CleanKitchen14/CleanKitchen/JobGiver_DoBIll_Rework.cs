using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;


namespace CleanKitchen
{
    //protected override IEnumerable<Toil> JobDriver_DoBill.MakeNewToils()
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    static class JobDriver_DoBill_MakeNewToils_CleanPatch
    {
        static MethodInfo LJumpIfTargetInsideBillGiver = AccessTools.Method(typeof(JobDriver_DoBill), "JumpIfTargetInsideBillGiver");

        static IEnumerable<Toil> DoMakeToils(JobDriver_DoBill __instance)
        {
            //normal scenario
            __instance.AddEndCondition(delegate
            {
                Thing thing = __instance.GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                if (thing is Building && !thing.Spawned)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            __instance.FailOnBurningImmobile(TargetIndex.A);
            __instance.FailOn(delegate ()
            {
                if (__instance.job.GetTarget(TargetIndex.A).Thing is Filth)
                    return false;

                IBillGiver billGiver = __instance.job.GetTarget(TargetIndex.A).Thing as IBillGiver;
                if (billGiver != null)
                {
                    if (__instance.job.bill.DeletedOrDereferenced)
                    {
                        return true;
                    }
                    if (!billGiver.CurrentlyUsableForBills())
                    {
                        return true;
                    }
                }
                return false;
            });
            bool placeInBillGiver = __instance.BillGiver is Building_MechGestator;
            Toil gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate ()
            {
                if (__instance.job.targetQueueB != null && __instance.job.targetQueueB.Count == 1)
                {
                    UnfinishedThing unfinishedThing = __instance.job.targetQueueB[0].Thing as UnfinishedThing;
                    if (unfinishedThing != null)
                    {
                        unfinishedThing.BoundBill = (Bill_ProductionWithUft)__instance.job.bill;
                    }
                }
                __instance.job.bill.Notify_DoBillStarted(__instance.pawn);
            };
            yield return toil;
            yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.B).NullOrEmpty<LocalTargetInfo>());

            // "started 10 jobs in one tick" fix from SmartMedicine: "Drop the [thing] so that you can then pick it up. Ya really."
            // https://github.com/alextd/RimWorld-SmartMedicine/blob/84e7ac3e84a7f68dd7c7ed493296c0f9d7103f8e/Source/InventorySurgery.cs#L72
            Toil DropTargetThingIfInInventory = ToilMaker.MakeToil("DropTargetThingIfInInventory");
            DropTargetThingIfInInventory.initAction = delegate
            {
                Pawn actor = DropTargetThingIfInInventory.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(TargetIndex.B).Thing;

                if (thing.holdingOwner != null)
                {
                    int count = /*curJob.count == -1 ? thing.stackCount :*/ Mathf.Min(curJob.count, actor.carryTracker.AvailableStackSpace(thing.def), thing.stackCount);
                    //Log.Message($"{actor}, {thing} ,count ({count}) = {curJob.count}, {actor.carryTracker.AvailableStackSpace(thing.def)}, {thing.stackCount}");
                    if (count < 1)
                        return;

                    var owner = thing.holdingOwner;
                    Map rootMap = ThingOwnerUtility.GetRootMap(owner.Owner);
                    IntVec3 rootPosition = ThingOwnerUtility.GetRootPosition(owner.Owner);
                    if (rootMap == null || !rootPosition.IsValid)
                        return;
                    //Log.Message($"{actor} trying to drop {thing}");
                    if (owner.TryDrop(thing, ThingPlaceMode.Near, count, out var droppedThing))
                    {
                        //Log.Message($"{actor} dropped {thing}");
                        curJob.SetTarget(TargetIndex.B, droppedThing);
                    }
                }
            };
            DropTargetThingIfInInventory.defaultCompleteMode = ToilCompleteMode.Instant;

            foreach (Toil toil2 in JobDriver_DoBill.CollectIngredientsToils(TargetIndex.B, TargetIndex.A, TargetIndex.C, false, true, __instance.BillGiver is Building_MechGestator))
            {
                yield return toil2;
                if (toil2.debugName == "JumpIfTargetInsideBillGiver")
                    yield return DropTargetThingIfInInventory;
            }

            //cleaning patch
            if (Settings.adv_cleaning && !Utility.IncapableOfCleaning(__instance.pawn))
            {
                Toil FilthList = new Toil();
                FilthList.initAction = delegate ()
                {
                    Job curJob = FilthList.actor.jobs.curJob;
                    if (curJob.GetTargetQueue(TargetIndex.A).NullOrEmpty())
                    {
                        LocalTargetInfo A = curJob.GetTarget(TargetIndex.A);

                        if (A.Thing?.TryGetComp<DoCleanComp>()?.Active != false)
                        {
                            IEnumerable<Filth> l = Utility.SelectAllFilth(FilthList.actor, A, Settings.adv_clean_num);
                            Utility.AddFilthToQueue(curJob, TargetIndex.A, l, FilthList.actor);
                            FilthList.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(TargetIndex.A), curJob);
                        }
                        curJob.targetQueueA.Add(A);
                    }
                };
                yield return FilthList;
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                Toil clean = ToilMaker.MakeToil("CleanBillPlace");
                clean.initAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    __instance.billStartTick = 0;
                    __instance.ticksSpentDoingRecipeWork = 0;
                    __instance.workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness;
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    __instance.billStartTick += 1;
                    __instance.ticksSpentDoingRecipeWork += 1;
                    if (__instance.billStartTick > filth.def.filth.cleaningWorkToReduceThickness)
                    {
                        filth.ThinFilth();
                        __instance.billStartTick = 0;
                        if (filth.Destroyed)
                        {
                            clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                            __instance.ReadyForNextToil();
                            return;
                        }
                    }
                };
                clean.defaultCompleteMode = ToilCompleteMode.Never;
                clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
                clean.WithProgressBar(TargetIndex.A, () => __instance.ticksSpentDoingRecipeWork / __instance.workLeft, true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList);
                clean.JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                yield return clean;
                yield return Toils_Jump.Jump(CleanFilthList);
            }

            //continuation of normal scenario
            yield return gotoBillGiver;
            yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();
            yield return Toils_Recipe.DoRecipeWork().FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
            yield return Toils_Recipe.FinishRecipeAndStartStoringProduct(TargetIndex.None);
            if (!__instance.job.RecipeDef.products.NullOrEmpty<ThingDefCountClass>() || !__instance.job.RecipeDef.specialProducts.NullOrEmpty<SpecialProductType>())
            {
                yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
                Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return carryToCell;
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true, true);
                var t = ToilMaker.MakeToil("MakeNewToils");
                t.initAction = delegate ()
                {
                    Bill_Production bill_Production = t.actor.jobs.curJob.bill as Bill_Production;
                    if (bill_Production != null && bill_Production.repeatMode == BillRepeatModeDefOf.TargetCount)
                    {
                        __instance.pawn.MapHeld.resourceCounter.UpdateResourceCounts();
                    }
                };
                yield return t;
            }
            yield break;
        }

        public static bool Prefix(ref IEnumerable<Toil> __result, ref JobDriver_DoBill __instance)
        {
            if (!Settings.adv_cleaning)
                return true;

            __result = DoMakeToils(__instance);
            return false;
        }
    }
}