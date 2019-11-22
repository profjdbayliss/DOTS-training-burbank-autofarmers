using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct MovementComponent : IComponentData
{
    public float2 targetPos;
    public float2 startPos;
    public float2 middlePos;
    public float speed;
}