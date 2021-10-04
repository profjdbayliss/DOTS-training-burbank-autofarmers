using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static Unity.Mathematics.math;


public class PlantSystem : SystemBase
{
    public static float MAX_GROWTH = 120.0f;
    private float deltaTime;
    public static NativeQueue<Entity> freePlants;
    public static NativeQueue<Entity> plantCreationDeletionInfo;
    public static NativeQueue<ComponentTransInfo> componentSetInfo;
    EntityQuery m_Group;
    
    public struct ComponentTransInfo
    {
        public Entity entity;
        public float3 trans;
    }

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery( typeof(PlantComponent),
            typeof(NonUniformScale), typeof(PlantTag));
        freePlants = new NativeQueue<Entity>(Allocator.Persistent);
        plantCreationDeletionInfo = new NativeQueue<Entity>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentTransInfo>(Allocator.Persistent);
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        if (freePlants.IsCreated)
        {
            freePlants.Dispose();
        }
        if (plantCreationDeletionInfo.IsCreated)
        {
            plantCreationDeletionInfo.Dispose();
        }
        if (componentSetInfo.IsCreated)
        {
            componentSetInfo.Dispose();
        }
        base.OnDestroy();
    }

    [BurstCompile]
    struct PlantSystemJob : IJobEntityBatch
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public ComponentDataFromEntity<Translation> translations;
        [ReadOnly] public float maxGrowth;
        public NativeQueue<Entity>.ParallelWriter plantChanges;
        public NativeQueue<ComponentTransInfo>.ParallelWriter setInfo;
        // chunk vars
        [ReadOnly] public EntityTypeHandle EntityType;
        public ComponentTypeHandle<PlantComponent> PlantComponentTypeHandle;
        public ComponentTypeHandle<NonUniformScale> NonUniformScaleTypeHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var plantComponents = batchInChunk.GetNativeArray(PlantComponentTypeHandle);
            var scales = batchInChunk.GetNativeArray(NonUniformScaleTypeHandle);
            var entities = batchInChunk.GetNativeArray(EntityType);
            
            for (var i = 0; i < batchInChunk.Count; i++)
            {
                PlantState state = (PlantState) plantComponents[i].state;

                switch (state)
                {
                    case PlantState.None:

                        break;
                    case PlantState.Growing:
                        float currentTotalTime = deltaTime + plantComponents[i].timeGrown;

                        if (currentTotalTime < maxGrowth)
                        {
                            float currentScale = currentTotalTime / 5.0f;
                            scales[i] = new NonUniformScale {Value = new float3(currentScale, 1.0f, currentScale)};
                            var data = new PlantComponent
                            {
                                timeGrown = currentTotalTime,
                                state = (int) PlantState.Growing,
                            };
                            plantComponents[i] = data;
                        }
                        else
                        {
                            var data = new PlantComponent
                            {
                                timeGrown = maxGrowth,
                                state = (int) PlantState.None,
                            };
                            plantComponents[i] = data;
                        }

                        break;
                    case PlantState.Following:
                        float3 pos = translations[plantComponents[i].farmerToFollow].Value;
                        float3 trans = new float3(pos.x, pos.y + 2, pos.z);
                        setInfo.Enqueue(new ComponentTransInfo
                        {
                            entity = entities[i],
                            trans = trans
                        });

                        break;
                    case PlantState.Deleted:
                        // multiple entities can try to delete the plant
                        // taken care of in the single threaded end of the jobs
                        
                        //UnityEngine.Debug.Log("deleting a plant " + entity.Index);
                        plantChanges.Enqueue(entities[i]);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        // // chunk vars
        // var plantType = GetComponentTypeHandle<PlantComponent>();
        // var scaleType = GetComponentTypeHandle<NonUniformScale>();
        // var entities = GetEntityTypeHandle();
        //
        // // job
        // var job = new PlantSystemJob();
        // job.deltaTime = UnityEngine.Time.deltaTime;
        // job.maxGrowth = MAX_GROWTH;
        // job.plantChanges = plantCreationDeletionInfo.AsParallelWriter();
        // job.translations = GetComponentDataFromEntity<Translation>(true);
        // job.PlantComponentTypeHandle = plantType;
        // job.NonUniformScaleTypeHandle = scaleType;
        // job.setInfo = componentSetInfo.AsParallelWriter();
        // job.EntityType = entities;
        //
        // int batchesPerChunk = 4;
        // this.Dependency = job.ScheduleParallel(m_Group, batchesPerChunk, this.Dependency);

        float deltaTime = UnityEngine.Time.deltaTime;
        float maxGrowth = MAX_GROWTH;
        var translations = GetComponentDataFromEntity<Translation>(true);
        var setInfo = componentSetInfo.AsParallelWriter();
        var plantChanges = plantCreationDeletionInfo.AsParallelWriter();
        JobHandle job = Entities.ForEach((Entity currentEntity, ref PlantComponent plant, ref NonUniformScale scale) => { 
            PlantState state = (PlantState) plant.state;

                switch (state)
                {
                    case PlantState.None:

                        break;
                    case PlantState.Growing:
                        float currentTotalTime = deltaTime + plant.timeGrown;

                        if (currentTotalTime < maxGrowth)
                        {
                            float currentScale = currentTotalTime / 5.0f;
                            scale = new NonUniformScale {Value = new float3(currentScale, 1.0f, currentScale)};
                            var data = new PlantComponent
                            {
                                timeGrown = currentTotalTime,
                                state = (int) PlantState.Growing,
                            };
                            plant = data;
                        }
                        else
                        {
                            var data = new PlantComponent
                            {
                                timeGrown = maxGrowth,
                                state = (int) PlantState.None,
                            };
                            plant = data;
                        }

                        break;
                    case PlantState.Following:
                        float3 pos = translations[plant.farmerToFollow].Value;
                        float3 trans = new float3(pos.x, pos.y + 2, pos.z);
                        setInfo.Enqueue(new ComponentTransInfo
                        {
                            entity = currentEntity,
                            trans = trans
                        });

                        break;
                    case PlantState.Deleted:
                        // multiple entities can try to delete the plant
                        // taken care of in the single threaded end of the jobs
                        //UnityEngine.Debug.Log("deleting a plant " + entity.Index);
                        plantChanges.Enqueue(currentEntity);
                        break;
                    default:
                        break;
                }
            
        }).WithReadOnly(translations).ScheduleParallel(this.Dependency);
        this.Dependency = job;
    }
}