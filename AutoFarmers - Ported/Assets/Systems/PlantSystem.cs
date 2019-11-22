﻿using Unity.Burst;
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

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    [BurstCompile]
    [RequireComponentTag(typeof(PlantTag))]
    struct PlantSystemJob : IJobForEachWithEntity<PlantComponent, NonUniformScale>
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public ComponentDataFromEntity<Translation> translations;
        [ReadOnly] public float maxGrowth;
        public EntityCommandBuffer.Concurrent ecb;

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
                    ecb.SetComponent(index, entity, new NonUniformScale { Value = new float3(currentScale, 1.0f, currentScale) });
                    var data = new PlantComponent
                    {
                        timeGrown = currentTotalTime,
                        state = (int)PlantState.Growing

                    };
                    ecb.SetComponent(index, entity, data);
                }
                else
                {
                    var data = new PlantComponent
                    {
                        timeGrown = maxGrowth,
                        state = (int)PlantState.None

                    };
                    ecb.SetComponent(index, entity, data);
                }
            } else if (plantComponent.state == (int)PlantState.Following)
            {
                float3 pos = translations[plantComponent.farmerToFollow].Value;
                ecb.SetComponent(index, entity, new Translation { Value = new float3(pos.x, pos.y+2, pos.z) });              
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

        return job.Schedule(this, inputDependencies);
    }
}