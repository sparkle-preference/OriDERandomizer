using UnityEngine;

public class CameraShakeLogic : MonoBehaviour, ISuspendable {
    public void Awake() {
        SuspensionManager.Register(this);
    }

    public void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public void UpdateOffset() {
        var shakeOffset = Vector3.zero;
        var shakeRotation = Vector3.zero;

        for (var i = 0; i < CameraShake.All.Count; i++) {
            var cameraShake = CameraShake.All[i];
            var modifiedStrength = cameraShake.ModifiedStrength;
            shakeOffset += cameraShake.CurrentOffset * modifiedStrength;
            shakeRotation += cameraShake.CurrentRotation * modifiedStrength;
        }

        shakeOffset *= RandomizerSettings.Accessibility.CameraShakeFactor;
        shakeRotation *= RandomizerSettings.Accessibility.CameraShakeFactor;

        Target.localPosition = shakeOffset;
        Target.localEulerAngles = shakeRotation;
    }

    public bool IsSuspended { get; set; }

    public Transform Target;
}
