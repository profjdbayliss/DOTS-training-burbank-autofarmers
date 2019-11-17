using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

//public class FindRockSystem : JobComponentSystem
//{
//    private EntityQuery m_RockQuery;
//    private EntityCommandBufferSystem ecbs;

//    protected override void OnCreate()
//    {
//        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
//        m_RockQuery = GetEntityQuery(new EntityQueryDesc
//        {
//            All = new[] { ComponentType.ReadOnly<RockTag>(), typeof(Translation) },

//        });
//    }

//    [RequireComponentTag(typeof(PerformRockTaskTag))]
//    [BurstCompile]
//    struct FindRockSystemJob : IJobForEachWithEntity<Translation, EntityInfo>
//    {
//        public EntityCommandBuffer.Concurrent ecb;
//        [ReadOnly] public ComponentDataFromEntity<RockTag> IsRockType;

//        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref EntityInfo rockInfo)
//        {

//            //Debug.Log("rock index is: " + rockInfo.specificRock.Index);
//            //Debug.Log("rock version is: " + rockInfo.specificRock.Version);
//            //Debug.Log("destroying a rock with location: " + translation.Value.x + " " + translation.Value.z);
//            if (rockInfo.type == (int)Tiles.Rock)
//            {
//                //Debug.Log("destroying rock");
//                ecb.DestroyEntity(rockInfo.specificEntity.Index, rockInfo.specificEntity);
//                ecb.RemoveComponent(index, entity, typeof(PerformRockTaskTag));
//                ecb.RemoveComponent(index, entity, typeof(EntityInfo));
//                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
//            }
//        }
//    }

//    protected override JobHandle OnUpdate(JobHandle inputDependencies)
//    {
//        var job = new FindRockSystemJob();
//        job.IsRockType = GetComponentDataFromEntity<RockTag>(true);
//        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();

//        return job.Schedule(this, inputDependencies);
//    }
//}