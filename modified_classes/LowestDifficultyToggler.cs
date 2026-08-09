using UnityEngine;

public class LowestDifficultyToggler : MonoBehaviour, IDebugMenuToggleable {
    public string Name => "Lowest Difficulty";

    public string HelpText => "Toggle lowest difficulty";

    public string[] ToggleOptions {
        get {
            return new[] {
                RandomizerText.DifficultyOverrides.Easy.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.Normal.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.Hard.NameOverride.ToString(),
                RandomizerText.DifficultyOverrides.OneLife.NameOverride.ToString()
            };
        }
    }

    public int CurrentToggleOptionId {
        get => (int)DifficultyController.Instance.LowestDifficulty;
        set => DifficultyController.Instance.LowestDifficulty = (DifficultyMode)((value % ToggleOptions.Length + ToggleOptions.Length) % ToggleOptions.Length);
    }

    private int m_currentOption;
}
