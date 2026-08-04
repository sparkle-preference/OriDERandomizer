using UnityEngine;

[Category("General")]
public class ActivateAction : ActionMethod {
    public void OnValidate() {
        if (Save && Target && Target.GetComponent<GameObjectActivator>()) {
            Save = false;
        }
    }

    public override void Perform(IContext context) {
        Target.SetActive(Activate);
    }

    public override void PerformInstantly(IContext context) {
        Perform(context);
    }

    public override void Serialize(Archive ar) {
        if (Save) {
            if (ar.Reading) {
                var active = ar.Serialize(true);
                if (Target) {
                    Target.SetActive(active);
                }
            }

            if (ar.Writing) {
                if (Target == null) {
                    ar.Serialize(false);
                } else {
                    ar.Serialize(Target.activeSelf);
                }
            }
        }
    }

    private string TargetName => !(Target != null) ? "unknown" : Target.name;

    public override string GetNiceName() {
        return (!Activate ? "Deactivate " : "Activate ") + TargetName;
    }

    [NotNull] public GameObject Target;

    public bool Activate = true;

    public bool Save = true;
}
