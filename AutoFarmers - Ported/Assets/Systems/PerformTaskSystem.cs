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

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        plantsSold = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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

        // var used by store:
        public NativeArray<int> plantsSold;
        //[ReadOnly] public ComponentDataFromEntity<Translation> translations;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref EntityInfo entityInfo)
        {
            Tiles state = (Tiles)entityInfo.type;

            switch (state)
            {
                case Tiles.Rock:
                    //Debug.Log("destroying rock");
                    ecb.DestroyEntity(entityInfo.specificEntity.Index, entityInfo.specificEntity);
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
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

                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    break;
                case Tiles.Plant:
                    // since the plant needs to be instantiated and then cached
                    // into the hash table it's done in the main thread
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
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
                                timeGrown = PlantSystem.MAX_GROWTH,
                                state = (int)PlantState.Following,
                                farmerToFollow = entity,
                                reserveIndex = plant.reserveIndex
                            };
                            ecb.SetComponent(entityInfo.specificEntity.Index,
                                 entityInfo.specificEntity, plantData2);
                        }
                        else if (plant.reserveIndex != entity.Index)
                        {
                            entityInfo.type = (short)Tiles.None;
                        }
                    } else
                    {
                        entityInfo.type = (short)Tiles.None;
                    }
                    
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    break;
                case Tiles.Store:
                    // since multiple entities can try to delete this one
                    // we need to make sure it exists first
                    //if (translations.Exists(entityInfo.specificEntity))
                    //{
                    // we need to remove the plant from the farmer
                    PlantComponent plantData = new PlantComponent
                    {
                        timeGrown = PlantSystem.MAX_GROWTH,
                        state = (int)PlantState.Deleted
                    };
                    ecb.SetComponent(entityInfo.specificEntity.Index,
                         entityInfo.specificEntity, plantData);
                    //}
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));

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
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);
        //jobHandle.Complete();

        return jobHandle;
    }
}