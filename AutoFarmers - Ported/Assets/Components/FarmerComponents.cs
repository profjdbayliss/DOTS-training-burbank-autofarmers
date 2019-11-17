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

