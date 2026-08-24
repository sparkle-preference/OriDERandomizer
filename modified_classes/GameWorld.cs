using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class GameWorld : SaveSerialize {
    public bool HasCompletedEverything() {
        bool flag = false;
        foreach (RuntimeGameWorldArea runtimeGameWorldArea in this.RuntimeAreas) {
            foreach (RuntimeWorldMapIcon runtimeWorldMapIcon in runtimeGameWorldArea.Icons) {
                WorldMapIconType icon = runtimeWorldMapIcon.Icon;
                switch (icon) {
                    case WorldMapIconType.HealthUpgrade:
                    case WorldMapIconType.EnergyUpgrade:
                    case WorldMapIconType.AbilityPoint:
                    case WorldMapIconType.Experience:
                    case WorldMapIconType.MapstonePickup:
                        break;
                    default:
                        if (icon != WorldMapIconType.Keystone) {
                            continue;
                        }
                        break;
                }
                flag = true;
            }
        }
        return !flag && this.CompletionPercentage == 100;
    }

    public void RevealIcon(MoonGuid icon) {
        this.m_revealedIcons.Add(icon);
    }

    public bool IconRevealed(MoonGuid icon) {
        return this.m_revealedIcons.Contains(icon);
    }

    public float CompletionAmount {
        get {
            int num = 0;
            float num2 = 0f;
            for (int i = 0; i < this.RuntimeAreas.Count; i++) {
                RuntimeGameWorldArea runtimeGameWorldArea = this.RuntimeAreas[i];
                num++;
                num2 += runtimeGameWorldArea.CompletionAmount;
            }
            return num2 / (float)num;
        }
    }

    public int CompletionPercentage {
        get {
            float completionAmount = this.CompletionAmount;
            if (Mathf.Approximately(completionAmount, 1f)) {
                return 100;
            }
            return Mathf.Clamp(Mathf.RoundToInt(this.CompletionAmount * 100f), 0, 99);
        }
    }

    public GameWorldArea FindAreaFromPosition(Vector3 position) {
        for (int i = 0; i < this.Areas.Count; i++) {
            GameWorldArea gameWorldArea = this.Areas[i];
            if (gameWorldArea.InsideFace(position)) {
                return gameWorldArea;
            }
        }
        return null;
    }

    public RuntimeGameWorldArea FindRuntimeArea(GameWorldArea area) {
        for (int i = 0; i < this.RuntimeAreas.Count; i++) {
            RuntimeGameWorldArea runtimeGameWorldArea = this.RuntimeAreas[i];
            if (runtimeGameWorldArea.Area == area) {
                return runtimeGameWorldArea;
            }
        }
        return null;
    }

    public override void Awake() {
        GameWorld.Instance = this;
        this.RuntimeAreas.Capacity = this.Areas.Count;
        for (int i = 0; i < this.Areas.Count; i++) {
            GameWorldArea gameWorldArea = this.Areas[i];
            this.RuntimeAreas.Add(new RuntimeGameWorldArea(gameWorldArea));
        }
        Events.Scheduler.OnGameReset.Add(new Action(this.OnGameReset));
        base.Awake();
    }

    public override void OnDestroy() {
        Events.Scheduler.OnGameReset.Remove(new Action(this.OnGameReset));
        base.OnDestroy();
    }

    public void OnGameReset() {
        for (int i = 0; i < this.RuntimeAreas.Count; i++) {
            RuntimeGameWorldArea runtimeGameWorldArea = this.RuntimeAreas[i];
            runtimeGameWorldArea.Initialize();
        }
        this.m_revealedIcons.Clear();
        this.ObjectiveText = null;
    }

    public GameWorldArea AreaFromIndex(int i) {
        if (i < 0 || i >= this.RuntimeAreas.Count) {
            return null;
        }
        return this.RuntimeAreas[i].Area;
    }

    public int IndexOfArea(GameWorldArea area) {
        return this.RuntimeAreas.FindIndex((RuntimeGameWorldArea a) => a.Area == area);
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            int num = 0;
            ar.Serialize(ref num);
            if (this.Areas.Count != num) {
                return;
            }
            int num2 = 0;
            while (num2 < num && num2 < this.RuntimeAreas.Count) {
                RuntimeGameWorldArea runtimeGameWorldArea = this.RuntimeAreas[num2];
                runtimeGameWorldArea.Serialize(ar);
                num2++;
            }
            this.m_revealedIcons.Clear();
            int num3 = ar.Serialize(0);
            for (int i = 0; i < num3; i++) {
                MoonGuid moonGuid = new MoonGuid(0, 0, 0, 0);
                moonGuid.Serialize(ar);
                this.m_revealedIcons.Add(moonGuid);
            }
            int num4 = ar.Serialize(0);
            if (num4 != -1) {
                this.ObjectiveText = this.ObjectiveTextProviders[num4];
            }
        } else {
            ar.Serialize(this.Areas.Count);
            for (int j = 0; j < this.RuntimeAreas.Count; j++) {
                RuntimeGameWorldArea runtimeGameWorldArea2 = this.RuntimeAreas[j];
                runtimeGameWorldArea2.Serialize(ar);
            }
            ar.Serialize(this.m_revealedIcons.Count);
            foreach (MoonGuid moonGuid2 in this.m_revealedIcons) {
                moonGuid2.Serialize(ar);
            }
            ar.Serialize(this.ObjectiveTextProviders.IndexOf(this.ObjectiveText));
        }
    }

    public void VisitMapAreasAtPosition(Vector3 currentPlayerPosition) {
        // When we are random spawning this ignores the default spawn location 
        // until we see something else.
        if (!isFirstLocationVisited && (Randomizer.SpawnScene != null)) {
            if (Vector3.Distance(currentPlayerPosition, spawnPosition) < 0.1) {
                return;
            }
        }
        for (int i = 0; i < RuntimeAreas.Count; i++) {
            var runtimeGameWorldArea = RuntimeAreas[i];
            runtimeGameWorldArea.VisitMapAreaAtPosition(currentPlayerPosition);
        }
        isFirstLocationVisited = true;
    }

    public GameWorldArea WorldAreaAtPosition(Vector3 worldPosition) {
        for (int i = 0; i < this.RuntimeAreas.Count; i++) {
            RuntimeGameWorldArea runtimeGameWorldArea = this.RuntimeAreas[i];
            Vector3 vector = runtimeGameWorldArea.Area.CageStructureTool.transform.InverseTransformPoint(worldPosition);
            CageStructureTool.Face face = runtimeGameWorldArea.Area.CageStructureTool.FindFaceAtPositionFaster(vector);
            if (face != null) {
                return runtimeGameWorldArea.Area;
            }
        }
        return null;
    }

    public static GameWorld Instance;

    public List<GameWorldArea> Areas = new List<GameWorldArea>();

    public List<RuntimeGameWorldArea> RuntimeAreas = new List<RuntimeGameWorldArea>();

    public RuntimeGameWorldArea CurrentArea;

    private readonly HashSet<MoonGuid> m_revealedIcons = new HashSet<MoonGuid>();

    public List<MessageProvider> ObjectiveTextProviders = new List<MessageProvider>();

    public MessageProvider ObjectiveText;

    private bool isFirstLocationVisited = false;

    private Vector3 spawnPosition = new Vector3(189.0f, -219.5f, 0.0f);
}
