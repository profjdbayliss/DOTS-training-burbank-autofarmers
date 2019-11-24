using System.Threading;
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
    private static NativeQueue<int> hashRemovals;

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


    protected override void OnDestroy()
    {
        if (hashRemovals.IsCreated)
        {
            hashRemovals.Dispose();
        }

        if (randomValues.IsCreated)
        {
            randomValues.Dispose();
        }
        base.OnDestroy();

    }

    public static void InitializeSearchSystem(int maxFarmers)
    {
        if (hashRemovals.IsCreated)
        {
            hashRemovals.Dispose();
        }
        else
        {
            hashRemovals = new NativeQueue<int>(Allocator.Persistent);
        }
    }


    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag))]
    struct SearchSystemJob : IJobForEachWithEntity<Translation, MovementComponent, EntityInfo>
    {
        public EntityCommandBuffer.Concurrent ecb;
        [ReadOnly] public NativeHashMap<int, EntityInfo> gridHashMap;
        [ReadOnly] public NativeArray<int> randArray;
        [ReadOnly] public int nextIndex;
        [ReadOnly] public int gridSize;
        [ReadOnly] public int radiusForSearch;
        // var specific to harvest task:
        [ReadOnly] public ComponentDataFromEntity<PlantComponent> IsPlantType;
        [ReadOnly] public float plantGrowthMax;
        // var for tilling
        public NativeQueue<int>.ParallelWriter removals;
        
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref MovementComponent movementComponent, ref EntityInfo entityIntent)
        {
            int taskValue;
            if (movementComponent.type == (int)MovementType.Farmer)
            {
                 taskValue = (randArray[(nextIndex + index) % randArray.Length] % 4) + 1;

                if (entityIntent.type == (int)Tiles.Harvest)
                {
                    // we just harvested and now need to get the plant
                    // to the store
                    taskValue = (int)Tiles.Store;
                }
            } else
            {
                // this is a drone: they only harvest and sell
                if (entityIntent.type == (int)Tiles.Harvest)
                {
                    // we just harvested and now need to get the plant
                    // to the store
                    taskValue = (int)Tiles.Store;
                } else
                {
                    taskValue = (int)Tiles.Harvest;
                }
            }

            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation;

            if (taskValue == (int)Tiles.Rock)
            {
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, taskValue, gridSize, gridSize);

            }
            else if (taskValue == (int)Tiles.Plant)
            {
                // need to search for tilled soil
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, (int)Tiles.Till, gridSize, gridSize);

            }
            else if (taskValue == (int)Tiles.Harvest)
            {
                // searches for the plants to go harvest them 
                foundLocation = 
                    GridData.FindMaturePlant(gridHashMap, pos, radiusForSearch, (int)Tiles.Plant, gridSize, gridSize,
                    ref IsPlantType, plantGrowthMax);
            }
            else if (taskValue == (int)Tiles.Store)
            {
                // searches for the stores to go and sell a plant 
                foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, (int)Tiles.Store, gridSize, gridSize);
                // no store close by
                if (foundLocation.x == -1)
                {
                    // need to find somewhere to get rid of the plant
                    foundLocation = GridData.Search(gridHashMap, pos, gridSize, (int)Tiles.Store, gridSize, gridSize);
                }

            }
            else // till only looks at things that don't exist in the grid - 0
            {
                Unity.Mathematics.Random rand;
                if ((uint)nextIndex == 0)
                {
                    rand = new Unity.Mathematics.Random(10);
                }
                else
                {
                    rand = new Unity.Mathematics.Random((uint)nextIndex);
                }

                // we look for a default spot to put a tilled thing
                float2 nextPos = new float2(Mathf.Abs(rand.NextInt()) % gridSize, Mathf.Abs(rand.NextInt()) % gridSize);
                foundLocation = GridData.Search(gridHashMap, nextPos, radiusForSearch, 0, gridSize, gridSize);

            }

            //Debug.Log("finding new task : " + taskValue + " for entity " + index + " found " + foundLocation.x);

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
                    var data = new MovementComponent { startPos = pos, speed = 2, targetPos = rockPos,
                        middlePos = findMiddle, type = movementComponent.type };
                    movementComponent = data;

                    // if we are on the way to the store then destroy the plant and
                    // mine the rock
                    if (taskValue == (int)Tiles.Store)
                    {
                        // destroy the plant as there's a rock in the way or no place to take it

                        //UnityEngine.Debug.Log("plant should be destroyed on farmer");
                        PlantComponent plantInfo = new PlantComponent
                        {
                            timeGrown = PlantSystem.MAX_GROWTH,
                            state = (int)PlantState.Deleted,
                        };
                        ecb.SetComponent(entityIntent.specificEntity.Index, entityIntent.specificEntity, plantInfo);
                    }

                    // get the index into the array of rocks so that we can find it
                    // to destroy it
                    EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                    entityIntent = fullRockData;
                    //ecb.SetComponent(index, entity, fullRockData);
                    //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));
                    int key = GridData.ConvertToHash((int)rockPos.x, (int)rockPos.y);
                    removals.Enqueue(key);
                }
                else
                {
                    foundLocation = new float2(foundLocation.x + 0.5f, foundLocation.y + 0.5f);
                    var data = new MovementComponent { startPos = pos, speed = 2, targetPos = foundLocation,
                        middlePos = findMiddle, type = movementComponent.type };
                    movementComponent = data;

                    //Debug.Log("doing a task and about to move: " + pos.x + " " + pos.y +
                    //    " target is : " + data.targetPos.x + " " + data.targetPos.y);
                    //Debug.Log("rock value: " + rockPos);
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));

                    if (taskValue == (int)Tiles.Rock)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        // get the index into the array of rocks so that we can find it
                        // to destroy it
                        EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                        //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                        entityIntent = fullRockData;
                        // remove the rock from the hash so that nobody else tries to get it
                        removals.Enqueue(key);
                    }
                    else if (taskValue == (int)Tiles.Plant)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        removals.Enqueue(key);
                        EntityInfo plantData = new EntityInfo { type = (int)Tiles.Plant };
                        entityIntent = plantData;
                    }
                    else if (taskValue == (int)Tiles.Harvest)
                    {
                        int key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                        EntityInfo fullData = GridData.getFullHashValue(gridHashMap, (int)foundLocation.x, (int)foundLocation.y);

                        // check to make sure plant is grown before harvesting
                        // if it's not then find something else to do
                        PlantComponent plantInfo = IsPlantType[fullData.specificEntity];
                        if (plantInfo.timeGrown >= plantGrowthMax)
                        {                            
                            removals.Enqueue(key);
                            EntityInfo harvestData = new EntityInfo { type = (int)Tiles.Harvest, specificEntity = fullData.specificEntity };
                            entityIntent = harvestData;
                            //Debug.Log("plant ready to harvest at : " + fullData.specificEntity.Index + " " + index + " " +
                            //    foundLocation.x + " " + foundLocation.y);
                        }
                        else
                        {
                            // not ready to harvest, try something else
                            ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                            ecb.RemoveComponent(index, entity, typeof(MovingTag));
                        }

                    }
                    else if (taskValue == (int)Tiles.Store)
                    {
                        EntityInfo storeInfo = new EntityInfo { type = (int)Tiles.Store,
                            specificEntity = entityIntent.specificEntity };
                        entityIntent = storeInfo;
                        //Debug.Log("plant going to the store " + foundLocation.x + " " + foundLocation.y + " " + entityIntent.specificEntity.Index);
                    }
                    else if (taskValue == (int)Tiles.Till)
                    {
                        EntityInfo tillData = new EntityInfo { type = (int)Tiles.Till };
                        entityIntent = tillData;
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
        EntityManager entityManager = World.Active.EntityManager;
        GridData data = GridData.GetInstance();
        int index = System.Math.Abs(rand.NextInt()) % randomValues.Length;
        var job = new SearchSystemJob();
        job.gridHashMap = data.gridStatus;
        job.randArray = randomValues;
        job.nextIndex = index;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.gridSize = data.width;
        job.radiusForSearch = 15;
        job.removals = hashRemovals.AsParallelWriter();
        job.IsPlantType = GetComponentDataFromEntity<PlantComponent>(true);
        job.plantGrowthMax = PlantSystem.MAX_GROWTH;

        var jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);

        // forced to sync here to remove all hash items at the same time
        jobHandle.Complete();

        // this happens in the main thread
        // removal isn't in the parallel writer for the hash table
        // so it's just going to have to happen sequentially

        while (hashRemovals.Count > 0)
        {
            int key = hashRemovals.Dequeue();
            EntityInfo value;
            if (data.gridStatus.TryGetValue(key, out value))
            {
                if (value.type == (int)Tiles.Till)
                {
                    float plantingHeight = 1.0f;
                    // we're planting here and need to add a plant entity
                    data.gridStatus.Remove(key);
                    float2 trans = new float2(GridData.getRow(key), GridData.getCol(key));
                    var instance = entityManager.Instantiate(GridDataInitialization.plantEntity);
                    EntityInfo plantInfo = new EntityInfo { type = (int)Tiles.Plant, specificEntity = instance };
                    if (data.gridStatus.TryAdd(key, plantInfo))
                    {
                        float3 pos = new float3((int)trans.x, plantingHeight, (int)trans.y);
                        entityManager.SetComponentData(instance, new Translation { Value = pos });
                        entityManager.SetComponentData(instance, new NonUniformScale { Value = new float3(1.0f, 1.0f, 1.0f) });
                        // for some reason the original plant mesh creation happens on the wrong axis, 
                        // so we have to rotate it 90 degrees
                        Rotation rotation = entityManager.GetComponentData<Rotation>(instance);
                        var newRot = rotation.Value * Quaternion.Euler(0, 0, 90);
                        entityManager.SetComponentData(instance, new Rotation { Value = newRot });
                        entityManager.SetComponentData(instance, new PlantComponent { timeGrown = 0, state = (int)PlantState.Growing,
                        });
                        //Debug.Log("added grid plant " + instance.Index);
                    }
                }
                else
                {
                    data.gridStatus.Remove(key);
                }
            }

        }

        return jobHandle;
    }

}