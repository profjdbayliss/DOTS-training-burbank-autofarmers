using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;
using Unity.Transforms;
using Unity.Collections;

public class GridData : MonoBehaviour
{
    const int BOARD_MULTIPLIER = 1000; // max board x and y size is 999
    const int ARRAY_MULTIPLIER = 10; // max number of statuses is 9

    public static int sX = 10;
    public static int sZ = 10;
    public static NativeHashMap<int, int> gridStatus;

    public GameObject TestCubePrefab;
    EntityManager em;

    public void Start()
    {
        gridStatus = new NativeHashMap<int, int>(100, Allocator.Persistent);

        gridStatus.TryAdd(ConvertToHash(4, 4), ConvertDataValue(1, 5));
        gridStatus.TryAdd(ConvertToHash(8, 8), ConvertDataValue(2, 6));
        gridStatus.TryAdd(ConvertToHash(15, 15), ConvertDataValue(3, 1));
        gridStatus.TryAdd(ConvertToHash(4, 4), ConvertDataValue(4, 2));
        //int temp = 0;
        //gridStatus.TryGetValue(ConvertToHash(7, 7), out temp);
        //float2 tmp = GridData.Search(new float2(7, 7), 5, 3);
        //Debug.Log("count that exists: " + tmp.x + " " + tmp.y);

        em = World.Active.EntityManager;
        CreateTestEntity();
    }

    //public void Update()
    //{
        
    //}

    public static void InitializeHashMap(int capacity)
    {
        if(gridStatus.IsCreated)
        {
            gridStatus.Dispose();
        }

        gridStatus = new NativeHashMap<int, int>(capacity, Allocator.Persistent);

    }

    public static int ConvertToHash(int row, int col)
    {
        return row * BOARD_MULTIPLIER + col;
    }

    public static int ConvertDataValue(int status, int arrayLocation)
    {
        return arrayLocation * ARRAY_MULTIPLIER + status;
    }

    public static int getArrayLocation(int dataValue)
    {
       return dataValue / ARRAY_MULTIPLIER;
    }

    public static int getStatus(int dataValue)
    {
        return dataValue - getArrayLocation(dataValue) * ARRAY_MULTIPLIER;
    }

    public static int getRow(int key)
    {
        return key / BOARD_MULTIPLIER;
    }

    public static int getCol(int key)
    {
        return key - getRow(key)* BOARD_MULTIPLIER;
    }

    public static float2 Search(NativeHashMap<int, int> hashMap, float2 currentPos, int radius, int statusToFind, int sizeX, int sizeZ)
    {
        int startX = (int)currentPos.x - radius;
        int startY = (int)currentPos.y - radius;
        if (startX < 0) startX = 0;
        if (startY < 0) startY = 0;

        int endX = (int)currentPos.x + radius+1;
        int endY = (int)currentPos.y + radius+1;
        if (endX > sizeX)
        {
            endX = sizeX;
           
        }
        if (endY > sizeZ)
        {
            endY = sizeZ;
        }

        int value = 0;
        for (int i = startX; i < endX; i++)
        {
            for (int j = startY; j < endY; j++)
            {
                hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value);
                if (getStatus(value) == statusToFind)
                {
                    return new float2(i, j);
                }
            }
        }
        return new float2(-1, -1);
    }



    void CreateTestEntity()
    {
        Entity testEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(TestCubePrefab, World.Active);
        em.AddComponentData(testEntity, new NeedsTaskTag { });
        //em.AddComponentData(testEntity, Translation {Value = new float3(5,0,5) });
        em.AddComponentData(testEntity, new Actor {targetPosition = new float2(0,0) });
        em.Instantiate(testEntity);
    }
}