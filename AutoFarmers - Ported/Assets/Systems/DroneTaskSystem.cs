using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class DroneTaskSystem : JobComponentSystem
{
    public EntityCommandBufferSystem ecbs;
    public NativeArray<int> randomValues;
    public Unity.Mathematics.Random rand;
    const int RANDOM_SIZE = 256;
    public static NativeQueue<RemovalInfo> hashRemovalsDrone;
    public static NativeQueue<ComponentSetInfo> componentSetInfo;
    public static NativeQueue<TagInfo> addRemoveTags;

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
        hashRemovalsDrone = new NativeQueue<RemovalInfo>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentSetInfo>(Allocator.Persistent);
        addRemoveTags = new NativeQueue<TagInfo>(Allocator.Persistent);

    }


    protected override void OnDestroy()
    {
        if (hashRemovalsDrone.IsCreated)
        {
            hashRemovalsDrone.Dispose();
        }

        if (randomValues.IsCreated)
        {
            randomValues.Dispose();
        }

        if (componentSetInfo.IsCreated)
        {
            componentSetInfo.Dispose();
        }
        if (addRemoveTags.IsCreated)
        {
            addRemoveTags.Dispose();
        }
        base.OnDestroy();

    }


    [BurstCompile]
    [RequireComponentTag(typeof(NeedsTaskTag), typeof(DroneTag))]
    struct DroneTaskSystemJob : IJobForEachWithEntity<Translation, MovementComponent, EntityInfo>
    {
        //public EntityCommandBuffer.Concurrent ecb;
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
        public NativeQueue<TagInfo>.ParallelWriter addRemoveTags;
        public NativeQueue<ComponentSetInfo>.ParallelWriter setInfo;

        // randomly determines a task and then finds the right tiles that
        // will help the task occur
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, ref MovementComponent movementComponent, ref EntityInfo entityIntent)
        {
            Tiles taskValue;

            //
            // determine what the task for this entity is
            //

            // this is a drone: they only harvest and sell
            if (entityIntent.type == (int)Tiles.Harvest)
            {
                // we just harvested and now need to get the plant
                // to the store
                taskValue = Tiles.Store;
            }
            else
            {
                taskValue = Tiles.Harvest;
            }

            //
            // look for the best tile for performing that task
            //
            float2 pos = new float2(translation.Value.x, translation.Value.z);
            float2 foundLocation;
            switch (taskValue)
            {
                case Tiles.Harvest:
                    // searches for the plants to go harvest them 
                    foundLocation =
                        GridData.FindMaturePlant(randArray, nextIndex, gridHashMap, pos, radiusForSearch, (int)Tiles.Plant, gridSize, gridSize,
                        ref IsPlantType, plantGrowthMax);
                    nextIndex++;
                    break;
                default:
                    // searches for the stores to go and sell a plant 
                    foundLocation = GridData.Search(randArray, nextIndex, gridHashMap, pos, radiusForSearch, (int)Tiles.Store, gridSize, gridSize);
                    // no store close by
                    if (foundLocation.x == -1)
                    {
                        nextIndex++;
                        // need to find somewhere to get rid of the plant
                        foundLocation = GridData.Search(randArray, nextIndex, gridHashMap, pos, gridSize, (int)Tiles.Store, gridSize, gridSize);
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
                float2 findMiddle = MovementSystem.FindMiddlePos(pos, foundLocation);
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
                        findMiddle = MovementSystem.FindMiddlePos(pos, rockPos);
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
                            timeGrown = plantGrowthMax,
                            state = (int)PlantState.Deleted,
                        };
                        //ecb.SetComponent(entityIntent.specificEntity.Index, entityIntent.specificEntity, plantInfo);
                        setInfo.Enqueue(new ComponentSetInfo {entity= entityIntent.specificEntity, plantComponent=plantInfo });
                    }

                    // get the index into the array of rocks so that we can find it
                    // to destroy it
                    EntityInfo fullRockData = GridData.getFullHashValue(gridHashMap, (int)rockPos.x, (int)rockPos.y);
                    entityIntent = fullRockData;
                    //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                    addRemoveTags.Enqueue(new TagInfo {shouldRemove=1,entity=entity,type=Tags.NeedsTask  });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.Moving });
                    //ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    //ecb.AddComponent(index, entity, typeof(MovingTag));
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
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.Moving });
                    //ecb.RemoveComponent(index, entity, typeof(NeedsTaskTag));
                    //ecb.AddComponent(index, entity, typeof(MovingTag));
                    int key;

                    switch (taskValue)
                    {
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
                                addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.NeedsTask });
                                addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.Moving });
                                //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                                //ecb.RemoveComponent(index, entity, typeof(MovingTag));
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

        var jobDrone = new DroneTaskSystemJob();
        jobDrone.gridHashMap = data.gridStatus;
        jobDrone.randArray = randomValues;
        jobDrone.nextIndex = index;
        //jobDrone.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        jobDrone.gridSize = data.width;
        jobDrone.radiusForSearch = 15;
        jobDrone.removals = hashRemovalsDrone.AsParallelWriter();
        jobDrone.IsPlantType = GetComponentDataFromEntity<PlantComponent>(true);
        jobDrone.plantGrowthMax = PlantSystem.MAX_GROWTH;
        jobDrone.addRemoveTags = addRemoveTags.AsParallelWriter();
        jobDrone.setInfo = componentSetInfo.AsParallelWriter();

        var jobHandleDrone = jobDrone.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandleDrone);

        return jobHandleDrone;
    }

}