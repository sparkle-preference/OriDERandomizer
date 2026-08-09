using UnityEngine;

[Category("Animator")]
public class AnimatorAction : ActionMethod {
    public new void Start() {
        base.Start();
        if (Target == null) {
            enabled = false;
            return;
        }

        if (AnimatorsMode == FindAnimatorsMode.GameObject) {
            Animators = Target.GetComponents<LegacyAnimator>();
        }

        if (AnimatorsMode == FindAnimatorsMode.GameObjectAndChildren) {
            Animators = Target.GetComponentsInChildren<LegacyAnimator>();
        }
    }

    public override void Perform(IContext context) {
        for (int i = 0; i < Animators.Length; i++) {
            LegacyAnimator legacyAnimator = Animators[i];
            if (legacyAnimator.enabled) {
                switch (Command) {
                    case PlayMode.Restart:
                        legacyAnimator.Restart();
                        break;
                    case PlayMode.RestartReversed:
                        legacyAnimator.RestartReverse();
                        break;
                    case PlayMode.Reverse:
                        legacyAnimator.Reverse();
                        break;
                    case PlayMode.Stop:
                        legacyAnimator.Stop();
                        break;
                    case PlayMode.Continue:
                        legacyAnimator.Continue();
                        break;
                    case PlayMode.ContinueForward:
                        legacyAnimator.Reversed = false;
                        legacyAnimator.Continue();
                        break;
                    case PlayMode.ContinueReversed:
                        legacyAnimator.Reversed = true;
                        legacyAnimator.Continue();
                        break;
                    case PlayMode.StopAtStart:
                        legacyAnimator.Restart();
                        legacyAnimator.Stop();
                        break;
                    case PlayMode.StopAtEnd:
                        legacyAnimator.RestartReverse();
                        legacyAnimator.Stop();
                        break;
                }

                legacyAnimator.Sample(legacyAnimator.CurrentTime);
            }
        }
    }

    public override void PerformInstantly(IContext context) {
        foreach (LegacyAnimator legacyAnimator in Animators) {
            if (legacyAnimator.enabled) {
                switch (Command) {
                    case PlayMode.Restart:
                        legacyAnimator.StopAndSampleAtEnd();
                        break;
                    case PlayMode.RestartReversed:
                        legacyAnimator.StopAndSampleAtStart();
                        break;
                    case PlayMode.Reverse:
                        legacyAnimator.StopAndSampleAtStart();
                        break;
                    case PlayMode.Stop:
                        legacyAnimator.Stop();
                        break;
                    case PlayMode.Continue:
                        if (legacyAnimator.Reversed) {
                            legacyAnimator.StopAndSampleAtStart();
                        } else {
                            legacyAnimator.StopAndSampleAtEnd();
                        }

                        break;
                    case PlayMode.ContinueForward:
                        legacyAnimator.StopAndSampleAtEnd();
                        break;
                    case PlayMode.ContinueReversed:
                        legacyAnimator.StopAndSampleAtStart();
                        break;
                    case PlayMode.StopAtStart:
                        legacyAnimator.StopAndSampleAtStart();
                        break;
                    case PlayMode.StopAtEnd:
                        legacyAnimator.StopAndSampleAtEnd();
                        break;
                }
            }
        }
    }

    private string TargetName {
        get { return AnimatorsMode != FindAnimatorsMode.SpecifyAnimators ? !Target ? "unkown" : Target.name : Animators.Length <= 0 || !Animators[0] ? "unkown" : Animators[0].name; }
    }

    public override string GetNiceName() {
        switch (Command) {
            case PlayMode.Restart:
                return "Restart " + TargetName + " animator";
            case PlayMode.RestartReversed:
                return "Restart reversed " + TargetName + " animator";
            case PlayMode.Reverse:
                return "Reverse " + TargetName + " animator";
            case PlayMode.Stop:
                return "Stop " + TargetName + " animator";
            case PlayMode.Continue:
                return "Continue " + TargetName + " animator";
            case PlayMode.ContinueForward:
                return "Continue " + TargetName + " animator forward";
            case PlayMode.ContinueReversed:
                return "Continue " + TargetName + " animator reversed";
            case PlayMode.StopAtStart:
                return "Stop " + TargetName + " animator at start";
            case PlayMode.StopAtEnd:
                return "Stop " + TargetName + " animator at end";
            default:
                return base.GetNiceName();
        }
    }

    [NotNull] public GameObject Target;

    public FindAnimatorsMode AnimatorsMode;

    public PlayMode Command;

    public LegacyAnimator[] Animators;

    public enum PlayMode {
        Restart,
        RestartReversed,
        Reverse,
        Stop,
        Continue,
        ContinueForward,
        ContinueReversed,
        StopAtStart,
        StopAtEnd
    }

    public enum FindAnimatorsMode {
        GameObject,
        GameObjectAndChildren,
        SpecifyAnimators
    }
}
