using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class PlantingSystem : JobComponentSystem
{
    private EntityCommandBufferSystem ecbs;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    [BurstCompile]
    [RequireComponentTag(typeof(PerformPlantingTaskTag))]
    public struct PlantingSystemJob : IJobForEachWithEntity<Translation, Rotation>
    {
        public EntityCommandBuffer.Concurrent ecb;
        [NativeDisableParallelForRestriction]
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction] public NativeHashMap<int, EntityInfo>.ParallelWriter grid;
        [ReadOnly] public Entity plantEntity;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref Rotation rotation)
        {
            float plantingHeight = 1.0f;
            EntityInfo plantInfo = new EntityInfo { type = (int)Tiles.Plant };
            if (
            grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
            plantInfo))
            {
                float3 pos = new float3((int)translation.Value.x, plantingHeight, (int)translation.Value.z);

                var instance = ecb.Instantiate(index, plantEntity);
                ecb.SetComponent(index, instance, new Translation { Value = pos });
                ecb.SetComponent(index, instance, new NonUniformScale { Value = new float3(1.0f, 1.0f, 1.0f) });
                // for some reason the original plant mesh creation happens on the wrong axis, 
                // so we have to rotate it 90 degrees
                var newRot = rotation.Value * Quaternion.Euler(0, 0, 90);
                ecb.SetComponent(index, instance, new Rotation { Value = newRot });
                ecb.SetComponent(index, instance, new PlantComponent { timeGrown = 0 });
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                ecb.RemoveComponent(index, entity, typeof(PerformPlantingTaskTag));
                //Debug.Log("added grid plant");
            }
            else
            {
                //Debug.Log("did not add to plant");
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                ecb.RemoveComponent(index, entity, typeof(PerformPlantingTaskTag));
            }


        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();
        var job = new PlantingSystemJob
        {
            ecb = ecbs.CreateCommandBuffer().ToConcurrent(),
            plantEntity = GridDataInitialization.plantEntity,
            grid = data.gridStatus.AsParallelWriter()
        };
        var jobHandle = job.ScheduleSingle(this, inputDependencies);
        ecbs.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }
}