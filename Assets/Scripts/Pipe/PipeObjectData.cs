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
    Beak = 1 << 1,
    Quack = 1 << 2,
}

public enum PipeInteractionKind
{
    Merge,
    Swap,
    Reject,
    Eject,
}

[CreateAssetMenu(fileName = "PipeObjectData", menuName = "Pipe/Pipe Object Data")]
public class PipeObjectData : ScriptableObject
{
    public PipeArchetype Archetype;
    public PipeObjectTag Tags = PipeObjectTag.None;
    public Sprite DisplaySprite;
    public GameObject SpawnPrefab;
    public PipeInteractionKind DefaultInteraction = PipeInteractionKind.Reject;
}
