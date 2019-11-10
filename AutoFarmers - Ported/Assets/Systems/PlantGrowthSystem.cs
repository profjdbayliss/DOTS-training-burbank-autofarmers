using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;


public class PlantGrowthSystem : JobComponentSystem
{
    public static float MAX_GROWTH = 120.0f;
    private EntityCommandBufferSystem ecbs;
    private float deltaTime;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    [BurstCompile]
    [RequireComponentTag(typeof(PlantTag))]
    struct PlantGrowthSystemJob : IJobForEachWithEntity<PlantComponent, NonUniformScale>
    {
        public float deltaTime;
        public EntityCommandBuffer.Concurrent ecb;
        public float maxGrowth;

        public void Execute(Entity entity, int index, 
            ref PlantComponent plantComponent,
            ref NonUniformScale scale)
        {

            float currentTotalTime = deltaTime + plantComponent.timeGrown;
            
            if (currentTotalTime < maxGrowth) {
                float currentScale = currentTotalTime / 10.0f;
                ecb.SetComponent(index, entity, new NonUniformScale { Value = new float3(currentScale, 1.0f, currentScale) });
                var data = new PlantComponent
                {
                    timeGrown = currentTotalTime,
                    
                };
                ecb.SetComponent(index, entity, data);
            }
            
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new PlantGrowthSystemJob();
        job.deltaTime = UnityEngine.Time.deltaTime;
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.maxGrowth = MAX_GROWTH;
 
        return job.Schedule(this, inputDependencies);
    }
}