using System;
using UnityEngine;

/// <summary>
/// Shared noise bus. Gunshots, swings, and screams wake every zombie in range.
/// Footsteps stay local on each zombie so a crowd does not spam the bus.
/// </summary>
public static class ZombieSense
{
    public enum Kind
    {
        Footstep,
        Sword,
        Gunshot,
        Scream
    }

    public struct Noise
    {
        public Vector3 position;
        public float radius;
        public Kind kind;
    }

    public static event Action<Noise> OnNoise;

    public static void Report(Vector3 position, Kind kind)
    {
        float radius;
        switch (kind)
        {
            case Kind.Footstep: radius = 8f; break;
            case Kind.Sword: radius = 14f; break;
            case Kind.Gunshot: radius = 38f; break;
            case Kind.Scream: radius = 30f; break;
            default: radius = 10f; break;
        }

        OnNoise?.Invoke(new Noise
        {
            position = position,
            radius = radius,
            kind = kind
        });
    }
}
