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

public class FarmerTaskSystem : JobComponentSystem
{
    public EntityCommandBufferSystem ecbs;
    public NativeArray<int> randomValues;
    public Unity.Mathematics.Random rand;
    const int RANDOM_SIZE = 256;
    public static NativeQueue<RemovalInfo> hashRemovalsFarmer;

    public struct RemovalInfo
    {
        public int key;
        public Entity requestingEntity;
    }

    protected override void OnCreate()
    {
        rand = new Unity.Mathematics.Random(42);
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        randomValues = new NativeArray<int>(RANDOM_SIZE, Allocator.Persistent);
        for (int i = 0; i < RANDOM_SIZE; i++)
        {
            randomValues[i] = System.Math.Abs(rand.NextInt());
        }
        hashRemovalsFarmer = new NativeQueue<RemovalInfo>(Allocator.Persistent);
    }


    protected override void OnDestroy()
    {
        if (hashRemovalsFarmer.IsCreated)
        {
            hashRemovalsFarmer.Dispose();
        }

        if (randomValues.IsCreated)
        {
            randomValues.Dispose();
        }
        base.OnDestroy();

    }
    

    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag), typeof(FarmerTag))]
    struct FarmerTaskSystemJob : IJobForEachWithEntity<Translation, MovementComponent, EntityInfo>
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
        public NativeQueue<RemovalInfo>.ParallelWriter removals;

        // randomly determines a task and then finds the right tiles that
        // will help the task occur
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref MovementComponent movementComponent, ref EntityInfo entityIntent)
        {
            Tiles taskValue;

            //
            // determine what the task for this entity is
            //

            taskValue = (Tiles)(randArray[(nextIndex + index) % randArray.Length] % 4) + 1;

            if (entityIntent.type == (int)Tiles.Harvest)
            {
                // we just harvested and now need to get the plant
                // to the store
                taskValue = Tiles.Store;
            }

            //
            // look for the best tile for performing that task
            //
            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation;
            switch (taskValue)
            {
                case Tiles.Rock:
                    foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, (int)taskValue, gridSize, gridSize);
                    break;
                case Tiles.Till:
                    // default is currently Till
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
                    break;
                case Tiles.Plant:
                    // need to search for tilled soil
                    foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, (int)Tiles.Till, gridSize, gridSize);
                    break;
                case Tiles.Harvest:
                    // searches for the plants to go harvest them 
                    foundLocation =
                        GridData.FindMaturePlant(gridHashMap, pos, radiusForSearch, (int)Tiles.Plant, gridSize, gridSize,
                        ref IsPlantType, plantGrowthMax);
                    break;
                default:
                    // searches for the stores to go and sell a plant 
                    foundLocation = GridData.Search(gridHashMap, pos, radiusForSearch, (int)Tiles.Store, gridSize, gridSize);
                    // no store close by
                    if (foundLocation.x == -1)
                    {
                        // need to find somewhere to get rid of the plant
                        foundLocation = GridData.Search(gridHashMap, pos, gridSize, (int)Tiles.Store, gridSize, gridSize);
                    }
                    break;
            }


            //Debug.Log("finding new task : " + taskValue + " for entity " + index + " found " + foundLocation.x);

            //
            // If a tile was found, set up the data for the task
            // if there's a rock in the way, then just go break the rock
            //
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
                    var data = new MovementComponent
                    {
                        startPos = pos,
                        speed = 2,
                        targetPos = rockPos,
                        middlePos = findMiddle,
                    };
                    movementComponent = data;

                    // if we are on the way to the store then destroy the plant and
                    // mine the rock
                    if (taskValue == Tiles.Store)
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
                    removals.Enqueue(new RemovalInfo { key = key, requestingEntity = entity });
                }
                else
                {
                    foundLocation = new float2(foundLocation.x + 0.5f, foundLocation.y + 0.5f);
                    var data = new MovementComponent
                    {
                        startPos = pos,
                        speed = 2,
                        targetPos = foundLocation,
                        middlePos = findMiddle,
                    };
                    movementComponent = data;

                    //Debug.Log("doing a task and about to move: " + pos.x + " " + pos.y +
                    //    " target is : " + data.targetPos.x + " " + data.targetPos.y);
                    //Debug.Log("rock value: " + rockPos);
                    ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.AddComponent(index, entity, typeof(MovingTag));
                    int key;

                    switch (taskValue)
                    {
                        case Tiles.Rock:
                            key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                            // get the index into the array of rocks so that we can find it
                            // to destroy it
                            EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                            //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                            entityIntent = fullRockData;
                            // remove the rock from the hash so that nobody else tries to get it
                            removals.Enqueue(new RemovalInfo { key = key, requestingEntity = entity });
                            break;
                        case Tiles.Till:
                            EntityInfo tillData = new EntityInfo { type = (int)Tiles.Till };
                            entityIntent = tillData;
                            break;
                        case Tiles.Plant:
                            key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                            removals.Enqueue(new RemovalInfo { key = key, requestingEntity = entity });
                            EntityInfo plantData = new EntityInfo { type = (int)Tiles.Plant };
                            entityIntent = plantData;
                            break;
                        case Tiles.Harvest:
                            key = GridData.ConvertToHash((int)foundLocation.x, (int)foundLocation.y);
                            EntityInfo fullData = GridData.getFullHashValue(gridHashMap, (int)foundLocation.x, (int)foundLocation.y);

                            // check to make sure plant is grown before harvesting
                            // if it's not then find something else to do
                            PlantComponent plantInfo = IsPlantType[fullData.specificEntity];

                            if (plantInfo.timeGrown >= plantGrowthMax)
                            {
                                removals.Enqueue(new RemovalInfo { key = key, requestingEntity = entity });
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
                            break;
                        case Tiles.Store:
                            EntityInfo storeInfo = new EntityInfo
                            {
                                type = (int)Tiles.Store,
                                specificEntity = entityIntent.specificEntity
                            };
                            entityIntent = storeInfo;
                            //Debug.Log("plant going to the store " + foundLocation.x + " " + foundLocation.y + " " + entityIntent.specificEntity.Index);
                            break;
                        default:

                            break;
                    }

                }
            }
            else
            {
                // no tile was found for the task, so this method
                // will run again and hopefully pick another task next time
                // Debug.Log("location wasn't found - find another task");
            }
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();
        int index = System.Math.Abs(rand.NextInt()) % randomValues.Length;
        var job = new FarmerTaskSystemJob();
        job.gridHashMap = data.gridStatus;
        job.randArray = randomValues;
        job.nextIndex = index;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.gridSize = data.width;
        job.radiusForSearch = 15;
        job.removals = hashRemovalsFarmer.AsParallelWriter();
        job.IsPlantType = GetComponentDataFromEntity<PlantComponent>(true);
        job.plantGrowthMax = PlantSystem.MAX_GROWTH;

        var jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);

        // forced to sync here to remove all hash items at the same time
        //jobHandle.Complete();
        //jobHandleDrone.Complete();

        return jobHandle;
    }

}