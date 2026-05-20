using UnityEngine;

public readonly struct PipeLaunchTarget
{
    public readonly bool IsHomerun;
    public readonly bool IsHomerunLeft;
    public readonly int SignedSlotOffset;
    public readonly Vector2 LandingPosition;
    public readonly PipeSlot TargetSlot;

    public int FlightDistanceSteps =>
        IsHomerun ? 1 : Mathf.Abs(SignedSlotOffset);

    private PipeLaunchTarget(
        bool isHomerun,
        bool isHomerunLeft,
        int signedSlotOffset,
        Vector2 landingPosition,
        PipeSlot targetSlot
    )
    {
        IsHomerun = isHomerun;
        IsHomerunLeft = isHomerunLeft;
        SignedSlotOffset = signedSlotOffset;
        LandingPosition = landingPosition;
        TargetSlot = targetSlot;
    }

    public static PipeLaunchTarget ForSlot(
        int signedOffset,
        PipeSlot slot,
        Vector2 landingPosition
    ) =>
        new(
            false,
            false,
            signedOffset,
            landingPosition,
            slot
        );

    public static PipeLaunchTarget ForHomerun(
        bool toLeft,
        Vector2 landingPosition
    ) =>
        new(
            true,
            toLeft,
            0,
            landingPosition,
            null
        );
}
