using Sein.World;
using UnityEngine;

public class SeinWorldStateCondition : Condition {
    public override bool Validate(IContext context) {
        switch (State) {
            case WorldState.WaterPurified:
                if (overrideEvent == OverrideEvents.None) {
                    return Events.WaterPurified == IsTrue;
                }

                if (overrideEvent == OverrideEvents.GinsoDoor) {
                    return false;
                }

                if (overrideEvent == OverrideEvents.WaterEscapeExit) {
                    var finishedEscape = Randomizer.Inventory.FinishedGinsoEscape;
                    surfaceColliders.SetActive(finishedEscape);
                    blockingWall.SetActive(finishedEscape);
                    if (finishedEscape) {
                        return finishedEscape == IsTrue;
                    }

                    return Events.WaterPurified == IsTrue;
                }

                if (overrideEvent == OverrideEvents.FinishEscapeTrigger) {
                    return Randomizer.Inventory.FinishedGinsoEscape;
                }

                return overrideEvent != OverrideEvents.False && Events.WaterPurified == IsTrue;
                break;
            case WorldState.GumoFree:
                return Events.GumoFree == IsTrue;
            case WorldState.SpiritTreeReached:
                return Events.SpiritTreeReached == IsTrue;
            case WorldState.GinsoTreeKey:
                return Keys.GinsoTree == IsTrue;
            case WorldState.WindRestored:
                return Randomizer.WindRestored() == IsTrue;
            case WorldState.GravityActivated:
                return Events.GravityActivated == IsTrue;
            case WorldState.MistLifted:
                return Events.MistLifted == IsTrue;
            case WorldState.ForlornRuinsKey:
                return Keys.ForlornRuins == IsTrue;
            case WorldState.MountHoruKey:
                return Keys.MountHoru == IsTrue;
            case WorldState.WarmthReturned:
                return Events.WarmthReturned == IsTrue;
            case WorldState.DarknessLifted:
                return Events.DarknessLifted == IsTrue;
        }

        return false;
    }

    private void Awake() {
        if (gameObject.name == "openingGinsoTree") {
            overrideEvent = OverrideEvents.GinsoDoor;
            return;
        }

        if (gameObject.name == "artAfter") {
            var transform4 = transform.FindChild("artAfter");
            var transform2 = transform4.FindChild("surfaceColliders");
            var transform3 = transform4.FindChild("blockingWall");
            if (transform2 && transform3) {
                overrideEvent = OverrideEvents.WaterEscapeExit;
                surfaceColliders = transform2.gameObject;
                blockingWall = transform3.gameObject;
            }
        } else {
            if (gameObject.name == "artBefore" && transform.parent && transform.parent.name == "ginsoTreeWaterRisingEnd") {
                overrideEvent = OverrideEvents.WaterEscapeExit;
                return;
            }

            if (name == "objectiveSetupTrigger" && transform.parent && transform.parent.name == "*objectiveSetup" && transform.parent.parent && transform.parent.parent.name == "thornfeltSwampActTwoStart") {
                overrideEvent = OverrideEvents.False;
                return;
            }

            if (name == "musiczones" && (transform.Find("musicZoneDuringRising") != null || (transform.parent && transform.parent.name == "ginsoTreeWaterRisingEnd"))) {
                overrideEvent = OverrideEvents.FinishEscapeTrigger;
                return;
            }

            if (name == "activator") {
                if (transform.childCount == 1 && transform.GetChild(0).name == "container" && transform.GetChild(0).childCount == 1 && transform.GetChild(0).GetChild(0).name == "musicZoneHeartWaterRising") {
                    overrideEvent = OverrideEvents.FinishEscapeTrigger;
                    return;
                }

                if (transform.childCount == 1 && transform.GetChild(0).name == "musicZoneWaterCleansed") {
                    overrideEvent = OverrideEvents.FinishEscapeTrigger;
                    return;
                }

                if (transform.parent && transform.parent.name == "restoringHeartWaterRising") {
                    overrideEvent = OverrideEvents.FinishEscapeTrigger;
                }
            }
        }
    }

    public WorldState State;

    public bool IsTrue = true;

    private OverrideEvents overrideEvent;

    private GameObject surfaceColliders;

    private GameObject blockingWall;

    private enum OverrideEvents {
        None,
        GinsoDoor,
        WaterEscapeExit,
        FinishEscapeTrigger,
        False,
    }
}
