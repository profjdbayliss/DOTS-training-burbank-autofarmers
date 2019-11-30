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
    public static NativeQueue<Entity> freePlants;
    public static NativeQueue<Entity> plantCreationDeletionInfo;
    public static NativeQueue<ComponentTransInfo> componentSetInfo;

    public struct ComponentTransInfo
    {
        public Entity entity;
        public float3 trans;
    }

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        freePlants = new NativeQueue<Entity>(Allocator.Persistent);
        plantCreationDeletionInfo = new NativeQueue<Entity>(Allocator.Persistent);
        componentSetInfo = new NativeQueue<ComponentTransInfo>(Allocator.Persistent);
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
    [RequireComponentTag(typeof(PlantTag))]
    struct PlantSystemJob : IJobForEachWithEntity<PlantComponent, NonUniformScale>
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public ComponentDataFromEntity<Translation> translations;
        [ReadOnly] public float maxGrowth;
        public EntityCommandBuffer.Concurrent ecb;
        public NativeQueue<Entity>.ParallelWriter plantChanges;
        public NativeQueue<ComponentTransInfo>.ParallelWriter setInfo;

        public void Execute(Entity entity, int index,
            ref PlantComponent plantComponent,
            ref NonUniformScale scale)
        {
            PlantState state = (PlantState)plantComponent.state;

            switch (state)
            {
                case PlantState.None:

                    break;
                case PlantState.Growing:
                    float currentTotalTime = deltaTime + plantComponent.timeGrown;

                    if (currentTotalTime < maxGrowth)
                    {
                        float currentScale = currentTotalTime / 10.0f;
                        scale = new NonUniformScale { Value = new float3(currentScale, 1.0f, currentScale) };
                        var data = new PlantComponent
                        {
                            timeGrown = currentTotalTime,
                            state = (int)PlantState.Growing,
                        };
                        plantComponent = data;
                    }
                    else
                    {
                        var data = new PlantComponent
                        {
                            timeGrown = maxGrowth,
                            state = (int)PlantState.None,
                        };
                        plantComponent = data;
                    }
                    break;
                case PlantState.Following:
                    float3 pos = translations[plantComponent.farmerToFollow].Value;
                    float3 trans = new float3(pos.x, pos.y + 2, pos.z);
                    setInfo.Enqueue(new ComponentTransInfo {entity = entity,
                        trans = trans 
                    });
                    //ecb.SetComponent(index, entity, new Translation { Value = new float3(pos.x, pos.y + 2, pos.z) });

                    break;
                case PlantState.Deleted:
                    // since multiple entities can try to delete this one
                    // we need to make sure it exists first
                    //if (translations.Exists(entity))
                    //{
                    //UnityEngine.Debug.Log("deleting a plant " + entity.Index);
                    //ecb.DestroyEntity(index, entity);
                    plantChanges.Enqueue(entity);
                    //}
                    break;
                default:
                    break;
            }

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        EntityManager entityManager = World.Active.EntityManager;

        var job = new PlantSystemJob();
        job.deltaTime = UnityEngine.Time.deltaTime;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.maxGrowth = MAX_GROWTH;
        job.plantChanges = plantCreationDeletionInfo.AsParallelWriter();
        job.translations = GetComponentDataFromEntity<Translation>(true);
        job.setInfo = componentSetInfo.AsParallelWriter();
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);
        //jobHandle.Complete();

        return jobHandle;
    }
}