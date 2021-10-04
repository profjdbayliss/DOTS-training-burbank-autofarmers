using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class PerformTaskSystem : JobComponentSystem
{
    public static NativeQueue<float2> tillChanges;
    public static Store storeInfo;
    public static NativeArray<int> plantsSold;
    public static NativeQueue<TagInfo> addRemoveTags;
    public static NativeQueue<ComponentSetInfo> componentSetInfo;
    EntityQuery m_Group;
    
    protected override void OnCreate()
    {
        plantsSold = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        addRemoveTags = new NativeQueue<TagInfo>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentSetInfo>(Allocator.Persistent);

        m_Group = GetEntityQuery( ComponentType.ReadOnly<Translation>(),
            typeof(EntityInfo), typeof(PerformTaskTag));
        base.OnCreate();
    }


    public static void InitializeTillSystem(int maxFarmers)
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }
        else
        {
            tillChanges = new NativeQueue<float2>(Allocator.Persistent);
        }
        
    }

    protected override void OnDestroy()
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }

        if (plantsSold.IsCreated)
        {
            plantsSold.Dispose();
        }
        if (addRemoveTags.IsCreated)
        {
            addRemoveTags.Dispose();
        }

        if (componentSetInfo.IsCreated)
        {
            componentSetInfo.Dispose();
        }
        base.OnDestroy();
    }
    
    [BurstCompile]
    struct PerformTaskSystemJob : IJobChunk
    {
        // var's used by multiple tasks
        [ReadOnly] public EntityCommandBuffer ecb;
        public NativeHashMap<int, EntityInfo>.ParallelWriter grid;

        // var's used by till task:
        public NativeQueue<float2>.ParallelWriter changes;

        // var's used by harvest
        [ReadOnly] public ComponentDataFromEntity<PlantComponent> plantInfo;
        [ReadOnly] public float plantGrowthMax;

        // var used by store:
        public NativeArray<int> plantsSold;
        public NativeQueue<TagInfo>.ParallelWriter addRemoveTags;
        public NativeQueue<ComponentSetInfo>.ParallelWriter setInfo;
        // chunk vars
        [ReadOnly] public ComponentTypeHandle<Translation> TranslationTypeHandle;
        [ReadOnly] public EntityTypeHandle EntityType;
        public ComponentTypeHandle<EntityInfo> EntityInfoHandle;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var translations = chunk.GetNativeArray(TranslationTypeHandle);
            var entityIntents = chunk.GetNativeArray(EntityInfoHandle);
            var entities = chunk.GetNativeArray(EntityType);
            
            for (var i = 0; i < chunk.Count; i++)
            {
                Tiles state = (Tiles) entityIntents[i].type;

                switch (state)
                {
                    case Tiles.Rock:
                        //Debug.Log("destroying rock");
                        // destroy rock
                        addRemoveTags.Enqueue(new TagInfo
                            {shouldRemove = 0, entity = entityIntents[i].specificEntity, type = Tags.Disable});
                        // remove perform task and add needs task tags
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.PerformTask});
                        break;
                    case Tiles.Till:
                        float tillBlockHeight = 0.25f;
                        EntityInfo tillInfo = new EntityInfo {type = (int) Tiles.Till};
                        if (
                            grid.TryAdd(GridData.ConvertToHash((int) translations[i].Value.x, (int) translations[i].Value.z),
                                tillInfo))
                        {
                            float3 pos = new float3((int) translations[i].Value.x, tillBlockHeight,
                                (int) translations[i].Value.z);

                            changes.Enqueue(new float2((int) pos.x, (int) pos.z));
                        }

                        // add needs task and remove perform task tags
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.PerformTask});
                        break;
                    case Tiles.Plant:
                        // since the plant needs to be instantiated and then cached
                        // into the hash table it's done in the main thread
                        // add needs task and remove perform task tags
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.PerformTask});

                        break;
                    case Tiles.Harvest:
                        EntityInfo harvestInfo = new EntityInfo {type = (int) Tiles.Till};
                        if (plantInfo.HasComponent(entityIntents[i].specificEntity))
                        {
                            PlantComponent plant = plantInfo[entityIntents[i].specificEntity];
                            if (plant.reserveIndex == entities[i].Index &&
                                grid.TryAdd(
                                    GridData.ConvertToHash((int) translations[i].Value.x, (int) translations[i].Value.z),
                                    harvestInfo))
                            {
                                //UnityEngine.Debug.Log("harvesting : " + entityIntents[i].specificEntity.Index);
                                
                                // plant needs to follow the farmer
                                PlantComponent plantData2 = new PlantComponent
                                {
                                    timeGrown = plantGrowthMax,
                                    state = (int) PlantState.Following,
                                    farmerToFollow = entities[i],
                                    reserveIndex = plant.reserveIndex
                                };
                                
                                setInfo.Enqueue(new ComponentSetInfo
                                {
                                    entity = entityIntents[i].specificEntity,
                                    plantComponent = plantData2
                                });

                            }
                            else if (plant.reserveIndex != entities[i].Index)
                            {
                                entityIntents[i] = new EntityInfo
                                {
                                    specificEntity = entityIntents[i].specificEntity,
                                    type = (short) Tiles.None
                                };
                            }
                        }
                        else
                        {
                            entityIntents[i] = new EntityInfo
                            {
                                specificEntity = entityIntents[i].specificEntity,
                                type = (short) Tiles.None
                            };
                        }

                        // remove perform task and add needs task
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.PerformTask});

                        break;
                    case Tiles.Store:
                        // multiple entities can try to delete this plant at the store
                        // the single threaded job at the end takes care of this
                        
                        // we need to remove the plant from the farmer
                        PlantComponent plantData = new PlantComponent
                        {
                            timeGrown = plantGrowthMax,
                            state = (int) PlantState.Deleted
                        };

                        setInfo.Enqueue(new ComponentSetInfo
                        {
                            entity = entityIntents[i].specificEntity,
                            plantComponent = plantData
                        });

                        // remove perform task and add needs task tags
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        addRemoveTags.Enqueue(new TagInfo {shouldRemove = 1, entity = entities[i], type = Tags.PerformTask});

                        // and actually sell stuff here
                        unsafe
                        {
                            Interlocked.Increment(ref ((int*) plantsSold.GetUnsafePtr())[0]);
                        }

                        break;
                    default:
                        break;
                }

            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();

        // chunk vars
        var translationType = GetComponentTypeHandle<Translation>(true);
        var entityInfoType = GetComponentTypeHandle<EntityInfo>();
        var entities = GetEntityTypeHandle();
        
        // job
        var job = new PerformTaskSystemJob();
        job.changes = tillChanges.AsParallelWriter();
        job.grid = data.gridStatus.AsParallelWriter();
        job.plantsSold = plantsSold;
        job.plantInfo = GetComponentDataFromEntity<PlantComponent>(true);
        job.addRemoveTags = addRemoveTags.AsParallelWriter();
        job.setInfo = componentSetInfo.AsParallelWriter();
        job.plantGrowthMax = PlantSystem.MAX_GROWTH;
        job.TranslationTypeHandle = translationType;
        job.EntityInfoHandle = entityInfoType;
        job.EntityType = entities;
        
        JobHandle jobHandle = job.ScheduleParallel(m_Group, inputDependencies);
        return jobHandle;

    }
}