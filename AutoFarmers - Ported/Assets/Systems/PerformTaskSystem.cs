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
    private EntityCommandBufferSystem ecbs;
    public static NativeQueue<float2> tillChanges;
    public static Store storeInfo;
    public static NativeArray<int> plantsSold;
    public static NativeQueue<TagInfo> addRemoveTags;
    public static NativeQueue<ComponentSetInfo> componentSetInfo;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        plantsSold = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        addRemoveTags = new NativeQueue<TagInfo>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentSetInfo>(Allocator.Persistent);
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

    [RequireComponentTag(typeof(PerformTaskTag))]
    [BurstCompile]
    struct PerformTaskSystemJob : IJobForEachWithEntity<Translation, EntityInfo>
    {
        // var's used by multiple tasks
        public EntityCommandBuffer.Concurrent ecb;
        public NativeHashMap<int, EntityInfo>.ParallelWriter grid;

        // var's used by till task:
        public NativeQueue<float2>.ParallelWriter changes;

        // var's used by harvest
        [ReadOnly] public ComponentDataFromEntity<PlantComponent> plantInfo;
        [ReadOnly] public float plantGrowthMax;

        // var used by store:
        public NativeArray<int> plantsSold;
        //[ReadOnly] public ComponentDataFromEntity<Translation> translations;
        public NativeQueue<TagInfo>.ParallelWriter addRemoveTags;
        public NativeQueue<ComponentSetInfo>.ParallelWriter setInfo;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref EntityInfo entityInfo)
        {
            Tiles state = (Tiles)entityInfo.type;

            switch (state)
            {
                case Tiles.Rock:
                    //Debug.Log("destroying rock");
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entityInfo.specificEntity, type = Tags.Disable });
                    //ecb.DestroyEntity(entityInfo.specificEntity.Index, entityInfo.specificEntity);
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.PerformTask });
                    //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    break;
                case Tiles.Till:
                    float tillBlockHeight = 0.25f;
                    EntityInfo tillInfo = new EntityInfo { type = (int)Tiles.Till };
                    if (
                    grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                    tillInfo))
                    {
                        float3 pos = new float3((int)translation.Value.x, tillBlockHeight, (int)translation.Value.z);

                        changes.Enqueue(new float2((int)pos.x, (int)pos.z));
                    }
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.PerformTask });

                    //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    break;
                case Tiles.Plant:
                    // since the plant needs to be instantiated and then cached
                    // into the hash table it's done in the main thread
                    //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.PerformTask });

                    break;
                case Tiles.Harvest:
                    EntityInfo harvestInfo = new EntityInfo { type = (int)Tiles.Till };
                    if (plantInfo.Exists(entityInfo.specificEntity))
                    {
                        PlantComponent plant = plantInfo[entityInfo.specificEntity];
                        if (plant.reserveIndex == entity.Index &&
                                grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                        harvestInfo))
                        {
                            //UnityEngine.Debug.Log("harvesting : " + entityInfo.specificEntity.Index);
                            // plant needs to follow the farmer

                            PlantComponent plantData2 = new PlantComponent
                            {
                                timeGrown = plantGrowthMax,
                                state = (int)PlantState.Following,
                                farmerToFollow = entity,
                                reserveIndex = plant.reserveIndex
                            };
                            setInfo.Enqueue(new ComponentSetInfo
                            {
                                entity = entityInfo.specificEntity,
                                plantComponent = plantData2
                            });

                            //ecb.SetComponent(entityInfo.specificEntity.Index,
                            //     entityInfo.specificEntity, plantData2);
                        }
                        else if (plant.reserveIndex != entity.Index)
                        {
                            entityInfo.type = (short)Tiles.None;
                        }
                    }
                    else
                    {
                        entityInfo.type = (short)Tiles.None;
                    }

                    //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.PerformTask });

                    break;
                case Tiles.Store:
                    // since multiple entities can try to delete this one
                    // we need to make sure it exists first
                    //if (translations.Exists(entityInfo.specificEntity))
                    //{
                    // we need to remove the plant from the farmer
                    PlantComponent plantData = new PlantComponent
                    {
                        timeGrown = plantGrowthMax,
                        state = (int)PlantState.Deleted
                    };
                    //ecb.SetComponent(entityInfo.specificEntity.Index,
                    //     entityInfo.specificEntity, plantData);
                    setInfo.Enqueue(new ComponentSetInfo
                    {
                        entity = entityInfo.specificEntity,
                        plantComponent = plantData
                    });

                    //}
                    //ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    //ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 0, entity = entity, type = Tags.NeedsTask });
                    addRemoveTags.Enqueue(new TagInfo { shouldRemove = 1, entity = entity, type = Tags.PerformTask });

                    // and should actually sell stuff here
                    unsafe
                    {
                        Interlocked.Increment(ref ((int*)plantsSold.GetUnsafePtr())[0]);
                    }
                    break;
                default:
                    break;
            }


        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();

        var job = new PerformTaskSystemJob();
        //job.IsRockType = GetComponentDataFromEntity<RockTag>(true);
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.changes = tillChanges.AsParallelWriter();
        job.grid = data.gridStatus.AsParallelWriter();
        job.plantsSold = plantsSold;
        //job.translations = GetComponentDataFromEntity<Translation>(true);
        job.plantInfo = GetComponentDataFromEntity<PlantComponent>(true);
        job.addRemoveTags = addRemoveTags.AsParallelWriter();
        job.setInfo = componentSetInfo.AsParallelWriter();
        job.plantGrowthMax = PlantSystem.MAX_GROWTH;
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);
        //jobHandle.Complete();

        return jobHandle;
    }
}