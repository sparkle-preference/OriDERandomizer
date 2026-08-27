// A row outside a layout's scroll window answers the game's own visibility question with
// no, so the layout skips it and nothing draws where there is no mask to clip it.
public class RandomizerScrollHide : Condition {
    public override bool Validate(IContext context) {
        return !Hidden && (Inner == null || Inner.Validate(context));
    }

    // Hiding a row also makes it report IsActivated false, and navigation skips those --
    // which would trap the selection inside the window it is meant to move.
    public static bool Hiding(CleverMenuItem item) {
        var hide = item.GetComponent<RandomizerScrollHide>();
        return hide != null && hide.Hidden;
    }

    // whatever the row already answered with, so this only ever narrows
    public Condition Inner;

    public bool Hidden;
}
