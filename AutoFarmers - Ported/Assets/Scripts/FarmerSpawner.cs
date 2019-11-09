using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class FarmerSpawner : MonoBehaviour
{

    public GameObject Prefab;
    public static Entity farmerEntity;
    public int farmerNumber;

    void Start()
    {
        // Create entity prefab from the game object hierarchy once
        farmerEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(Prefab, World.Active);
        var entityManager = World.Active.EntityManager;

        // Efficiently instantiate a bunch of entities from the already converted entity prefab
        
        Unity.Mathematics.Random rand = new Unity.Mathematics.Random(42);
        GridData gdata = GridData.GetInstance();

        for (int i = 0; i < farmerNumber; i++)
        {
            var instance = entityManager.Instantiate(farmerEntity);
            int startX = Math.Abs(rand.NextInt()) % gdata.width;
            int startZ = Math.Abs(rand.NextInt()) % gdata.width;

            // Place the instantiated entity in a grid with some noise
            var position = new float3(startX, 2, startZ);
            //var position = transform.TransformPoint(new float3(0,0,0));
            entityManager.SetComponentData(instance, new Translation() { Value = position });
            var data = new actor_RunTimeComp { startPos = new float2(startX, startZ), speed = 2, targetPos = new float2(startX, startZ) };
            entityManager.SetComponentData(instance, data);
            // give his first command based on the 1's in the hash
            entityManager.AddComponent<NeedsTaskTag>(instance);
        }
    }
}

