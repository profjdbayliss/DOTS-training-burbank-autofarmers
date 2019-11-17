using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MovementComponent : IComponentData
{
    public float2 targetPos;
    public float2 startPos;
    public float2 middlePos;
    public float speed;
}

[Serializable]
public struct IntentionComponent : IComponentData
{
    public int intent;
}

//[Serializable]
//public enum Intentions : int { None = 0, Rock = 1, Till = 2, Plant = 3, Store = 4, PerformRock = 5, PerformTill = 6, PerformPlanting = 7, MovingToHarvest=8, PerformHarvest=9, MovingToStore = 11 };


