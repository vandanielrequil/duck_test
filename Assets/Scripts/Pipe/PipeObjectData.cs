using UnityEngine;

public enum PipeArchetype
{
    Duck,
    Fake,
    Modifier,
}

[System.Flags]
public enum PipeObjectTag
{
    None = 0,

    Yellow = 1 << 0,
    Red = 1 << 1,
    Blue = 1 << 2,

    Beak = 1 << 3,
    Quack = 1 << 4,

    Paint = 1 << 5,
    Heavy = 1 << 6,
}

public enum PipeInteractionKind
{
    Move,
    Merge,
    Shove,
    Bounce,
    Reject,
    Eject,
}

[CreateAssetMenu(
    fileName = "PipeObjectData",
    menuName = "Pipe/Pipe Object Data"
)]
public class PipeObjectData : ScriptableObject
{
    [Header("Identity")]
    public string ObjectId;

    public PipeArchetype Archetype;

    public PipeObjectTag Tags =
        PipeObjectTag.None;

    [Header("Visual")]
    public Sprite DisplaySprite;

    public GameObject SpawnPrefab;

    [Header("Gameplay")]
    public int Weight = 1;

    [Tooltip(
        "Flight speed only (shorter flight). Does not change which slot the batter selects."
    )]
    public float LaunchAccelModifier = 1f;

    public bool CanBeMerged = true;

    public bool CanBeShoved = true;

    public PipeInteractionKind
        DefaultInteraction =
            PipeInteractionKind.Reject;

    [Tooltip("Used by legacy helpers only; batter aim ignores this.")]
    public float ThrowPower =>
        LaunchAccelModifier
        / Mathf.Max(1, Weight);
}
