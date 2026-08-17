using UnityEngine;
using UnityEngine.Serialization;

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

public enum PipeModifierType
{
    None,
    Paint,
    Kit,
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
    [FormerlySerializedAs("DisplaySprite")]
    public GameObject DisplayVisualPrefab;

    [Header("Fake merge visuals")]
    [FormerlySerializedAs("FakeSprite")]
    public GameObject FakeVisualPrefab;
    [FormerlySerializedAs("FakePaintSprite")]
    public GameObject FakePaintVisualPrefab;
    [FormerlySerializedAs("FakeKitSprite")]
    public GameObject FakeKitVisualPrefab;
    [FormerlySerializedAs("FakePaintKitSprite")]
    public GameObject FakePaintKitVisualPrefab;

    [Header("Poses")]
    public Sprite IdleSprite;
    public Sprite FlySprite;
    public Sprite LandingSprite;
    public Sprite[] HappySprites = System.Array.Empty<Sprite>();
    public Sprite[] SurprisedSprites = System.Array.Empty<Sprite>();

    [Header("Gameplay")]
    [Range(0, 3)]
    public int BaseDuckiness;

    public PipeModifierType ModifierType =
        PipeModifierType.None;

    public float FollowSpeed = 8f;

    [Min(1)]
    public int Weight = 1;

    [Header("Flight Visuals")]
    [Tooltip(
        "Visual flight speed only. Does not change how many slots the object flies."
    )]
    [FormerlySerializedAs("LaunchAccelModifier")]
    public float FlightSpeedMultiplier = 1f;

    [Tooltip("Visual flight arc height multiplier.")]
    public float FlightArcMultiplier = 1f;

    [Tooltip("Minimum seconds between clicks on this object. 0 = no cooldown.")]
    [Min(0f)]
    public float ClickCooldown = 0f;

    public bool CanBeMerged = true;

    public bool CanBeShoved = true;

    public PipeInteractionKind
        DefaultInteraction =
            PipeInteractionKind.Reject;

    private void OnValidate()
    {
        Weight = Mathf.Max(1, Weight);

        if (BaseDuckiness > 0)
            return;

        BaseDuckiness = Archetype switch
        {
            PipeArchetype.Duck => 3,
            _ => 0,
        };
    }
}
