using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;


public class PlantSystem : JobComponentSystem
{
    public static float MAX_GROWTH = 120.0f;
    private EntityCommandBufferSystem ecbs;
    private float deltaTime;
    private static NativeQueue<PlantDataSet> plantDataSet;
    private static NativeQueue<PlantTranslations> plantTranslations;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        plantDataSet = new NativeQueue<PlantDataSet>(Allocator.Persistent);
        plantTranslations = new NativeQueue<PlantTranslations>(Allocator.Persistent);

    }

    public struct PlantDataSet
    {
        public Entity entity;
        public PlantComponent plantData;
        public NonUniformScale scale;
    }

    public struct PlantTranslations
    {
        public Entity entity;
        public Translation translation;
    }

    protected override void OnDestroy()
    {
        if (plantDataSet.IsCreated)
        {
            plantDataSet.Dispose();
        }
        if (plantTranslations.IsCreated)
        {
            plantTranslations.Dispose();
        }
        base.OnDestroy();
    }

    [BurstCompile]
    [RequireComponentTag(typeof(PlantTag))]
    struct PlantSystemJob : IJobForEachWithEntity<PlantComponent, NonUniformScale>
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public ComponentDataFromEntity<Translation> translations;
        [ReadOnly] public float maxGrowth;
        public EntityCommandBuffer.Concurrent ecb;
        public NativeQueue<PlantDataSet>.ParallelWriter plantDataSet;
        public NativeQueue<PlantTranslations>.ParallelWriter plantTranslations;

        public void Execute(Entity entity, int index, 
            ref PlantComponent plantComponent,
            ref NonUniformScale scale)
        {
            if (plantComponent.state == (int)PlantState.None)
            {
                return;
            }
            else if (plantComponent.state == (int)PlantState.Growing)
            {
                float currentTotalTime = deltaTime + plantComponent.timeGrown;

                if (currentTotalTime < maxGrowth)
                {
                    float currentScale = currentTotalTime / 10.0f;
                    NonUniformScale newScale = new NonUniformScale { Value = float3(currentScale, 1.0f, currentScale) };
                    //ecb.SetComponent(index, entity, new NonUniformScale { Value = new float3(currentScale, 1.0f, currentScale) });
                    var data = new PlantComponent
                    {
                        timeGrown = currentTotalTime,
                        state = (int)PlantState.Growing

                    };
                    plantDataSet.Enqueue(new PlantDataSet { entity = entity, plantData= data, scale = newScale });
                    // FIX : ecb doesn't like setting this data
                    //ecb.SetComponent(index, entity, data);
                }
                else
                {
                    var data = new PlantComponent
                    {
                        timeGrown = maxGrowth,
                        state = (int)PlantState.None

                    };
                    plantDataSet.Enqueue(new PlantDataSet { entity = entity, plantData = data, scale = scale });
                    // FIX : ecb doesn't like setting this data
                    //ecb.SetComponent(index, entity, data);
                }
            } else if (plantComponent.state == (int)PlantState.Following)
            {
                float3 pos = translations[plantComponent.farmerToFollow].Value;
                Translation trans = new Translation { Value = new float3(pos.x, pos.y + 2, pos.z) };
                plantTranslations.Enqueue(new PlantTranslations { entity = entity, translation = trans });
                // FIX : ecb doesn't like setting this data
                //ecb.SetComponent(index, entity, new Translation { Value = new float3(pos.x, pos.y+2, pos.z) });              
            }
            else if (plantComponent.state == (int)PlantState.Deleted)
            {
                ecb.DestroyEntity(index, entity);
            }
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new PlantSystemJob();
        job.deltaTime = UnityEngine.Time.deltaTime;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.maxGrowth = MAX_GROWTH;
        job.translations = GetComponentDataFromEntity<Translation>(true);
        job.plantDataSet = plantDataSet.AsParallelWriter();
        job.plantTranslations = plantTranslations.AsParallelWriter();
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        jobHandle.Complete();
        EntityManager entityManager = World.Active.EntityManager;
        while (plantDataSet.Count > 0)
        {
            PlantDataSet data = plantDataSet.Dequeue();
            entityManager.SetComponentData(data.entity, data.plantData);
            entityManager.SetComponentData(data.entity, data.scale);
        }
        while (plantTranslations.Count > 0)
        {
            PlantTranslations data = plantTranslations.Dequeue();
            entityManager.SetComponentData(data.entity, data.translation);
        }

        return jobHandle;
    }
}