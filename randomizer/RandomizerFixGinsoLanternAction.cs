using UnityEngine;

public class RandomizerFixGinsoLanternAction : ActionMethod {
    public override void Perform(IContext context) {
        if (joint.connectedAnchor.magnitude > 1.0f) {
            joint.connectedAnchor = new Vector3(-0.6572f, -0.0351f, 0f);
        }
    }
    public ConfigurableJoint joint;
}
