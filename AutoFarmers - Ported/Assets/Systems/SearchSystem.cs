using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Movement;
using static Unity.Mathematics.math;

public class SearchSystem : JobComponentSystem
{
    public EntityCommandBufferSystem ecbs;
    public NativeArray<int> randomValues;
    public Unity.Mathematics.Random rand;
    const int RANDOM_SIZE = 256;

    protected override void OnCreate()
    {
        rand = new Unity.Mathematics.Random(42);
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        randomValues = new NativeArray<int>(RANDOM_SIZE, Allocator.Persistent);
        for (int i = 0; i < RANDOM_SIZE; i++)
        {
            randomValues[i] = System.Math.Abs(rand.NextInt());
        }

    }

    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag))]
    struct SearchSystemJob : IJobForEachWithEntity<Translation, MovementComponent, IntentionComponent>
    {
        public EntityCommandBuffer.Concurrent ecb;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction] public NativeHashMap<int, EntityInfo> gridHashMap;
        [ReadOnly] public NativeArray<int> randArray;
        [ReadOnly] public int nextIndex;
        [ReadOnly] public int gridSize;
        [ReadOnly] public int radiusForSearch;

        //public enum Intentions : int { None = 0, Rock = 1, Till = 2, Plant = 3, Store = 4, PerformRock = 5, PerformTill = 6, PerformPlanting = 7, MovingToStore = 11 };
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref MovementComponent movementComponent, ref IntentionComponent entityIntent)
        {
            // magic numbers remaining - till radius and the 3 in task value
            int TILL_RADIUS = 5;
            int taskValue = (randArray[(nextIndex + index) % randArray.Length] % 3) + 1;
            //Debug.Log("finding new task : " + taskValue);
            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation;

            if (taskValue == (int)Intentions.Rock)
            {
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, taskValue, gridSize, gridSize);
                
            }
            else if (taskValue == (int)Intentions.Plant)
            {
                // need to search for 2 which is the tilled soil
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, 2, gridSize, gridSize);

            }
            else if (taskValue == (int)Intentions.Store)
            {
                // searches for the plants to go harvest them - 3
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, 3, gridSize, gridSize);

            }
            else // till only looks at things that don't exist in the grid - 0
            {
                Unity.Mathematics.Random rand = new Unity.Mathematics.Random((uint)nextIndex);
                // we look for a default spot to put a tilled thing
                float2 nextPos = new float2(Mathf.Abs(rand.NextInt()) % gridSize, Mathf.Abs(rand.NextInt()) % gridSize);
                foundLocation = GridData.Search(gridHashMap, nextPos, TILL_RADIUS, 0, gridSize, gridSize);

            }
            if (foundLocation.x != -1 && foundLocation.y != -1)
            {
                float2 findMiddle = MovementJob.FindMiddlePos(pos, foundLocation);
                var rockPos = GridData.FindTheRock(gridHashMap, pos, findMiddle, foundLocation, gridSize, gridSize);
                //Debug.Log(index + " Start: " + pos.x + " " + pos.y + " middle : " + findMiddle.x + " " + findMiddle.y + " target pos : " +
                //    foundLocation.x + " " + foundLocation.y + " " + rockPos + " intention: " + taskValue);
                if ((int)rockPos.x != -1 && (int)rockPos.y != -1)
                {
                    // we found a rock so go mine it on the path
                    // if rock position on an x or y direction then don't change the middle
                    // otherwise re-find the middle
                    if ((int)rockPos.x == (int)pos.x || (int)rockPos.y == (int)pos.y)
                    {
                        findMiddle = MovementJob.FindMiddlePos(pos, rockPos);
                    }
                    //Debug.Log("Updated rock position to: " + rockPos + "Actor is now chasing a rock");

                    rockPos = new float2(rockPos.x + 0.5f, rockPos.y + 0.5f);
                    var data = new MovementComponent { startPos = pos, speed = 2, targetPos = rockPos, middlePos = findMiddle};
                    var intention = new IntentionComponent { intent = (int)Tiles.Rock };
                    // get the index into the array of rocks so that we can find it
                    // to destroy it
                    EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                    ecb.AddComponent(index, entity, fullRockData);
                    //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);

                    movementComponent = data;
                    entityIntent = intention;
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));
                    int key = GridData.ConvertToHash((int)rockPos.x, (int)rockPos.y);
                    gridHashMap.Remove(key);
                }
                else
                {

                    foundLocation = new float2(foundLocation.x + 0.5f, foundLocation.y + 0.5f);

                    var data = new MovementComponent { startPos = pos, speed = 2, targetPos = foundLocation, middlePos = findMiddle };
                    ecb.SetComponent(index, entity, data);
                    var intention = new IntentionComponent { intent = taskValue };
                    ecb.SetComponent(index, entity, intention);

                    //Debug.Log("doing a task and about to move: " + pos.x + " " + pos.y +
                    //    " target is : " + data.targetPos.x + " " + data.targetPos.y);
                    //Debug.Log("rock value: " + rockPos);
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));

                    if (taskValue == (int)Intentions.Rock)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        // get the index into the array of rocks so that we can find it
                        // to destroy it
                        EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                        //int rockEntityIndex = GridData.getArrayLocation(fullRockData);
                        //Entity tmp = rocks[rockEntityIndex];
                        //RockInfo rockEntityInfo = new RockInfo { specificRock = tmp };
                        //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                        ecb.AddComponent(index, entity, fullRockData);
                        // remove the rock from the hash so that nobody else tries to get it
                        gridHashMap.Remove(key);
                    }
                    if (taskValue == (int)Intentions.Plant)
                    {

                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        gridHashMap.Remove(key);
                    }
                    if (taskValue == (int)Intentions.Store)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        gridHashMap.Remove(key);
                       // Debug.Log("removed plant from location" + foundLocation);
                    }


                }
            }
            else
            {
               // Debug.Log("location wasn't found - find another task");
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();
        int index = System.Math.Abs(rand.NextInt()) % randomValues.Length;
        var job = new SearchSystemJob();
        job.gridHashMap = data.gridStatus;
        job.randArray = randomValues;
        job.nextIndex = index;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.gridSize = data.width;
        job.radiusForSearch = 15;

        //Debug.Log("nextInt: " + (randomValues[(index) % randomValues.Length]%4 + 1));
        var jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);

     
        return jobHandle;
    }

    protected override void OnDestroy()
    {
        if (randomValues.IsCreated)
        {
            randomValues.Dispose();
        }
        base.OnDestroy();
    }
}