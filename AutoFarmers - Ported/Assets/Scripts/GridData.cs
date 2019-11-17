using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Data structure: hash table with Entity information per tile position
// where it exists since it's a sparse data set for the majority
// of the sim
public class GridData 
{
    private static GridData data = null;

    const int BOARD_MULTIPLIER = 1000; // max board x and y size is 999
                                        // x and y are just concatenated to make the key

    public int width = 10;
    public NativeHashMap<int, EntityInfo> gridStatus;

    public static GridData GetInstance()
    {
        if (data != null)
        {
            return data;
        } else
        {
            data = new GridData();
            return data;
        }
        

        //gridStatus.TryAdd(ConvertToHash(5, 4), ConvertDataValue(2, 1));
        //gridStatus.TryAdd(ConvertToHash(8, 5), ConvertDataValue(2, 2));
        //gridStatus.TryAdd(ConvertToHash(7, 4), ConvertDataValue(1, 3));
        //gridStatus.TryAdd(ConvertToHash(5, 15), ConvertDataValue(1, 3));
        //gridStatus.TryAdd(ConvertToHash(8, 15), ConvertDataValue(3, 3));

        // test find the rock
        //float2 value = FindTheRock(gridStatus, new float2(5, 4), new float2(8, 4), new float2(8, 5), sX, sZ);
        //Debug.Log("the rock test: " + value.x + " " + value.y);



        //gridStatus.TryAdd(ConvertToHash(4, 4), ConvertDataValue(4, 2));
        //int temp = 0;
        //gridStatus.TryGetValue(ConvertToHash(7, 7), out temp);
        //float2 tmp = GridData.Search(new float2(7, 7), 5, 3);
        //Debug.Log("count that exists: " + tmp.x + " " + tmp.y);
        
    }


    public void OnDestroy()
    {
        if (gridStatus.IsCreated)
        {
            gridStatus.Dispose();

        }
    }

    // the board width is the capacity and needs to be multiplied by itself
    // to get the true every space capacity because capacity
    // is assumed just to be the width of the board
    public void Initialize(int capacity)
    {
        if(gridStatus.IsCreated)
        {
            gridStatus.Dispose();
        }

        gridStatus = new NativeHashMap<int, EntityInfo>(capacity*capacity, Allocator.Persistent);
        this.width = capacity;
    }

    // creates a key from the row/col of a tile location
    public static int ConvertToHash(int row, int col)
    {
        return row * BOARD_MULTIPLIER + col;
    }

    public static EntityInfo getFullHashValue(NativeHashMap<int, EntityInfo> hashMap, int row, int col)
    {
        EntityInfo returnValue;
        hashMap.TryGetValue(GridData.ConvertToHash(row, col), out returnValue);       
        return returnValue;
    }

    // the row and col are concatenated to form the key
    public static int getRow(int key)
    {
        return key / BOARD_MULTIPLIER;
    }

    // the row and col are concatenated to form the key
    public static int getCol(int key)
    {
        return key - getRow(key)* BOARD_MULTIPLIER;
    }

    // assumes good data input for positions and is not checking for positions off the board
    public static float2 FindTheRock(NativeHashMap<int, EntityInfo> hashMap, float2 currentPos, float2 middlePos, float2 targetPos, int sizeX, int sizeZ)
    {
        int ROCK = (int)Tiles.Rock;
        int startX = (int)currentPos.x;
        int startY = (int)currentPos.y;
        int endX = (int)middlePos.x;
        int endY = (int)middlePos.y;

        int i = startX;
        int j = startY;
        int countEnd = 0;
        EntityInfo value;
        if (endX-startX != 0)
        {
            countEnd = endX;
            // this is the dir we're searching
            if (endX < startX)
            {
                for (; i >= countEnd; i--)
                {
                    //Debug.Log("rock: " + i + " " + j + " " + value);
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        //Debug.Log("found something!");
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }
            else
            {
                for (; i <= countEnd; i++)
                {
                    //Debug.Log("rock2: " + i + " " + j + " " + value);
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        //Debug.Log("found something!");
                        
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }

        } else
        {
            // this is the dir we're searching
            i = startX;
            j = startY;
            countEnd = endY;
            if (endY < startY)
            {
                for (; j >= countEnd; j--)
                {
                    //Debug.Log("rock3: " + i + " " + j + " " + value);
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        //Debug.Log("found something!");

                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }
            else
            {
                for (; j <= countEnd; j++)
                {
                   // Debug.Log("rock4: " + i + " " + j + " " + value);
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        //Debug.Log("found something!");

                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }
 
        }

        //Debug.Log("part way through the method");

        // no rocks on path to middle, so try path from middle to end
        startX = (int)middlePos.x;
        startY = (int)middlePos.y;
        endX = (int)targetPos.x;
        endY = (int)targetPos.y;

        i = startX;
        j = startY;
        countEnd = 0;
        if (endX - startX != 0)
        {
            // this is the dir we're searching
            if (endX < startX)
            {
                countEnd = endX;
                for (; i >= countEnd; i--)
                {
                    //Debug.Log("rock5: " + i + " " + j + " " + value);

                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }
            else
            {
                countEnd = endX;
                for (; i <= countEnd; i++)
                {
                    //Debug.Log("rock6: " + i + " " + j + " " + value);

                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }

        }
        else
        {
            countEnd = endY;
            // this is the dir we're searching
            if (endY < startY)
            {
                for (; j >= countEnd; j--)
                {
                    //Debug.Log("rock7: " + i + " " + j + " " + value);

                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }
            else
            {
                for (; j <= countEnd; j++)
                {
                    //Debug.Log("rock8: " + i + " " + j + " " + value);

                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == ROCK)
                        {
                            return new float2(i, j);
                        }
                    }
                }
            }

        }

        // no rocks means this return
        return new float2(-1, -1);
    }

    // looks for a particular status id in a surrounding square radius
    // from a position
    // Doesn't look for the best position, looks for the first randomly
    // starting either at the first part of the array of locations or
    // from the end to the first
    public static float2 Search(NativeHashMap<int, EntityInfo> hashMap, float2 currentPos, int radius, int statusToFind, int sizeX, int sizeZ)
    {
        Unity.Mathematics.Random rand;
        if ((uint)currentPos.x == 0)
        {
            rand = new Unity.Mathematics.Random(10);
        }
        else
        {
            rand = new Unity.Mathematics.Random((uint)currentPos.x);
        }
        
        int startX = (int)currentPos.x - radius;
        int startY = (int)currentPos.y - radius;
        if (startX < 0) startX = 0;
        if (startY < 0) startY = 0;

        int endX = (int)currentPos.x + radius+1;
        int endY = (int)currentPos.y + radius+1;
        if (endX >= sizeX)
        {
            endX = sizeX-1;
           
        }
        if (endY >= sizeZ)
        {
            endY = sizeZ-1;
        }

        EntityInfo value;
        if ((Mathf.Abs(rand.NextInt())%100) > 50)
        {
            //Debug.Log("positive search position");
            for (int i = startX; i <= endX; i++)
            {
                for (int j = startY; j <= endY; j++)
                {
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == statusToFind)
                        {
                            return new float2(i, j);
                        }
                    }
                    else if (statusToFind == 0)
                    {
                        return new float2(i, j);
                    }
                }
            }
        } else
        {
            //Debug.Log("negative search direction");
            for (int i = endX; i >= startX; i--)
            {
                for (int j = endY; j >= startY; j--)
                {
                    if (hashMap.TryGetValue(GridData.ConvertToHash(i, j), out value))
                    {
                        if (value.type == statusToFind)
                        {
                            return new float2(i, j);
                        }
                    }
                    else if (statusToFind == 0)
                    {
                        return new float2(i, j);
                    }
                }
            }
        }
        return new float2(-1, -1);
    }

}