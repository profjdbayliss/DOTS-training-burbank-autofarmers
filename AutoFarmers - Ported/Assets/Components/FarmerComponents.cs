using System;
using Unity.Entities;
using Unity.Mathematics;

public struct MovementSetData
{
    public Entity entity;
    public MovementComponent movementData;
}

[Serializable]
public struct MovementComponent : IComponentData
{
    public float2 targetPos;
    public float2 startPos;
    public float2 middlePos;
    public float speed;
    public int myType;
}

public enum MovingType {  Farmer = 0, Drone = 1 };