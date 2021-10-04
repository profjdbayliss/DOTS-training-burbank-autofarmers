using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class DroneTaskSystem : SystemBase
{
    public NativeArray<int> randomValues;
    public Unity.Mathematics.Random rand;
    const int RANDOM_SIZE = 256;
    public static NativeQueue<RemovalInfo> hashRemovalsDrone;
    public static NativeQueue<ComponentSetInfo> componentSetInfo;
    public static NativeQueue<TagInfo> addRemoveTags;
    EntityQuery m_Group;
    
    public struct RemovalInfo
    {
        public int key;
        public Entity requestingEntity;

    }

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery( ComponentType.ReadOnly<Translation>(),
            typeof(MovementComponent), typeof(EntityInfo),
            typeof(NeedsTaskTag), typeof(DroneTag));
        rand = new Unity.Mathematics.Random(42);
        randomValues = new NativeArray<int>(RANDOM_SIZE, Allocator.Persistent);
        for (int i = 0; i < RANDOM_SIZE; i++)
        {
            randomValues[i] = System.Math.Abs(rand.NextInt());
        }
        hashRemovalsDrone = new NativeQueue<RemovalInfo>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentSetInfo>(Allocator.Persistent);
        addRemoveTags = new NativeQueue<TagInfo>(Allocator.Persistent);
        base.OnCreate();
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
    struct DroneTaskSystemJob : IJobChunk
    {
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
        // chunk vars
        [ReadOnly] public ComponentTypeHandle<Translation> TranslationTypeHandle;
        [ReadOnly] public EntityTypeHandle EntityType;
        public ComponentTypeHandle<MovementComponent> MovementTypeHandle;
        public ComponentTypeHandle<EntityInfo> EntityInfoHandle;

        // randomly determines a task for the drone and then finds the right tiles that
        // will help the task occur
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var translations = chunk.GetNativeArray(TranslationTypeHandle);
            var movements = chunk.GetNativeArray(MovementTypeHandle);
            var entityIntents = chunk.GetNativeArray(EntityInfoHandle);
            var entities = chunk.GetNativeArray(EntityType);

            for (var i = 0; i < chunk.Count; i++)
            {

                Tiles taskValue;

                //
                // determine what the task for this entity is
                //

                // this is a drone: they only harvest and sell
                if (entityIntents[i].type == (int) Tiles.Harvest)
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
                float2 pos = new float2(translations[i].Value.x, translations[i].Value.z);
                float2 foundLocation;
                switch (taskValue)
                {
                    case Tiles.Harvest:
                        // searches for the plants to go harvest them 
                        foundLocation =
                            GridData.FindMaturePlant(randArray, nextIndex, gridHashMap, pos, radiusForSearch,
                                (int) Tiles.Plant, gridSize, gridSize,
                                ref IsPlantType, plantGrowthMax);
                        nextIndex++;
                        break;
                    default:
                        // searches for the stores to go and sell a plant 
                        foundLocation = GridData.Search(randArray, nextIndex, gridHashMap, pos, radiusForSearch,
                            (int) Tiles.Store, gridSize, gridSize);
                        // no store close by
                        if (foundLocation.x == -1)
                        {
                            nextIndex++;
                            // need to find somewhere to get rid of the plant
                            foundLocation = GridData.Search(randArray, nextIndex, gridHashMap, pos, gridSize,
                                (int) Tiles.Store, gridSize, gridSize);
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
                    if ((int) rockPos.x != -1 && (int) rockPos.y != -1)
                    {
                        // we found a rock so go mine it on the path
                        // if rock position on an x or y direction then don't change the middle
                        // otherwise re-find the middle
                        if ((int) rockPos.x == (int) pos.x || (int) rockPos.y == (int) pos.y)
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
                        movements[i]= data;

                        // if we are on the way to the store then destroy the plant and
                        // mine the rock
                        if (taskValue == Tiles.Store)
                        {
                            // destroy the plant as there's a rock in the way or no place to take it

                            //UnityEngine.Debug.Log("plant should be destroyed on drone");
                            PlantComponent plantInfo = new PlantComponent
                            {
                                timeGrown = plantGrowthMax,
                                state = (int) PlantState.Deleted,
                            };
                            
                            setInfo.Enqueue(new ComponentSetInfo
                                {entity = entityIntents[i].specificEntity, plantComponent = plantInfo});
                        }

                        // get the index into the array of rocks so that we can find it
                        // to destroy it
                        EntityInfo fullRockData =
                            GridData.getFullHashValue(gridHashMap, (int) rockPos.x, (int) rockPos.y);
                        entityIntents[i] = fullRockData;
                        //Debug.Log("rock task happening : " + rockEntityIndex + " " + tmp.Index);
                        // remove needstask and add moving tags
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.Moving});
                        
                        int key = GridData.ConvertToHash((int) rockPos.x, (int) rockPos.y);
                        removals.Enqueue(new RemovalInfo {key = key, requestingEntity = entities[i]});
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
                        movements[i] = data;

                        //Debug.Log("doing a task and about to move: " + pos.x + " " + pos.y +
                        //    " target is : " + data.targetPos.x + " " + data.targetPos.y);
                        //Debug.Log("rock value: " + rockPos);
                        // remove needstask and add moving tag
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.Moving});
                        
                        int key;

                        switch (taskValue)
                        {
                            case Tiles.Harvest:
                                key = GridData.ConvertToHash((int) foundLocation.x, (int) foundLocation.y);
                                EntityInfo fullData = GridData.getFullHashValue(gridHashMap, (int) foundLocation.x,
                                    (int) foundLocation.y);

                                // check to make sure plant is grown before harvesting
                                // if it's not then find something else to do
                                PlantComponent plantInfo = IsPlantType[fullData.specificEntity];

                                if (plantInfo.timeGrown >= plantGrowthMax)
                                {
                                    removals.Enqueue(new RemovalInfo {key = key, requestingEntity = entities[i]});
                                    EntityInfo harvestData = new EntityInfo
                                        {type = (int) Tiles.Harvest, specificEntity = fullData.specificEntity};
                                    entityIntents[i] = harvestData;
                                    //Debug.Log("plant ready to harvest at : " + fullData.specificEntity.Index + " " + index + " " +
                                    //    foundLocation.x + " " + foundLocation.y);
                                }
                                else
                                {
                                    // not ready to harvest, try something else
                                    // add needstask and remove moving tag
                                    addRemoveTags.Enqueue(new TagInfo
                                        {shouldRemove = 1, entity = entities[i], type = Tags.NeedsTask});
                                    addRemoveTags.Enqueue(new TagInfo
                                        {shouldRemove = 0, entity = entities[i], type = Tags.Moving});
                                }

                                break;
                            case Tiles.Store:
                                EntityInfo storeInfo = new EntityInfo
                                {
                                    type = (int) Tiles.Store,
                                    specificEntity = entityIntents[i].specificEntity
                                };
                                entityIntents[i] = storeInfo;
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
    }

    protected override void OnUpdate()
    {
        GridData data = GridData.GetInstance();
        int index = System.Math.Abs(rand.NextInt()) % randomValues.Length;

        var translationType = GetComponentTypeHandle<Translation>(true);
        var movementType = GetComponentTypeHandle<MovementComponent>();
        var entityInfoType = GetComponentTypeHandle<EntityInfo>();
        var entities = GetEntityTypeHandle();
        
        var jobDrone = new DroneTaskSystemJob();
        jobDrone.gridHashMap = data.gridStatus;
        jobDrone.randArray = randomValues;
        jobDrone.nextIndex = index;
        jobDrone.gridSize = data.width;
        jobDrone.radiusForSearch = data.width/2;
        jobDrone.removals = hashRemovalsDrone.AsParallelWriter();
        jobDrone.IsPlantType = GetComponentDataFromEntity<PlantComponent>(true);
        jobDrone.plantGrowthMax = PlantSystem.MAX_GROWTH;
        jobDrone.addRemoveTags = addRemoveTags.AsParallelWriter();
        jobDrone.setInfo = componentSetInfo.AsParallelWriter();
        jobDrone.TranslationTypeHandle = translationType;
        jobDrone.MovementTypeHandle = movementType;
        jobDrone.EntityInfoHandle = entityInfoType;
        jobDrone.EntityType = entities;

        this.Dependency = jobDrone.ScheduleParallel(m_Group, this.Dependency);
    }

}