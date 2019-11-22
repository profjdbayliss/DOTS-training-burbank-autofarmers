using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class PerformTaskSystem : JobComponentSystem
{
    private EntityCommandBufferSystem ecbs;
    private static NativeQueue<float2> tillChanges;

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
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
    }

    [RequireComponentTag(typeof(PerformTaskTag))]
    [BurstCompile]
    struct PerformTaskSystemJob : IJobForEachWithEntity<Translation, Rotation, EntityInfo>
    {
        // var's used by multiple tasks
        public EntityCommandBuffer.Concurrent ecb;
        public NativeHashMap<int, EntityInfo>.ParallelWriter grid;

        // var specific to rock tasks:
        [ReadOnly] public ComponentDataFromEntity<RockTag> IsRockType;

        // var's used by till task:
        public NativeQueue<float2>.ParallelWriter changes;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref Rotation rotation, ref EntityInfo entityInfo)
        {

            if (entityInfo.type == (int)Tiles.Rock)
            {
                //Debug.Log("destroying rock");
                ecb.DestroyEntity(entityInfo.specificEntity.Index, entityInfo.specificEntity);
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Till)
            {
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
            }
            else if (entityInfo.type == (int)Tiles.Plant)
            {
                // since the plant needs to be instantiated and then cached
                // into the hash table it's done in the main thread
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Harvest)
            {
                EntityInfo tillInfo = new EntityInfo { type = (int)Tiles.Till };

                if (grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                tillInfo))
                {
                    // plant needs to follow the farmer
                    PlantComponent plantInfo = new PlantComponent
                    {
                        timeGrown = PlantSystem.MAX_GROWTH,
                        state = (int)PlantState.Following,
                        farmerToFollow = entity
                    };
                    ecb.SetComponent(entityInfo.specificEntity.Index,
                         entityInfo.specificEntity, plantInfo);
                }
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            }

            else if (entityInfo.type == (int)Tiles.Store)
            {
                // we need to remove the plant from the farmer
                PlantComponent plantInfo = new PlantComponent
                {
                    timeGrown = PlantSystem.MAX_GROWTH,
                    state = (int)PlantState.Deleted
                };

                ecb.SetComponent(entityInfo.specificEntity.Index,
                     entityInfo.specificEntity, plantInfo);
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));

                // and should actually sell stuff here

            }

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();

        var job = new PerformTaskSystemJob();
        job.IsRockType = GetComponentDataFromEntity<RockTag>(true);
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.changes = tillChanges.AsParallelWriter();
        job.grid = data.gridStatus.AsParallelWriter();
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        jobHandle.Complete();

        // we have to have a sync point here since we're about to change all uv's for the frame
        // This happens on main thread since uv's are Vector2 types and can't be changed inside of the job
        while (tillChanges.Count > 0)
        {
            float2 pos = tillChanges.Dequeue();
            if ((int)pos.x != -1 && (int)pos.y != -1)
            {
                // set the uv's on the mesh
                // NOTE: set pos to be a specific number if you want to test it
                Mesh tmp = GridDataInitialization.getMesh((int)pos.x, (int)pos.y,
                    GridDataInitialization.BoardWidth);
                int width = GridDataInitialization.getMeshWidth(tmp, (int)pos.x,
                    (int)pos.y, GridDataInitialization.BoardWidth);
                //Debug.Log("changing uv at! " + pos + " " + width );

                Vector2[] uv = tmp.uv;
                TextureUV tex = GridDataInitialization.textures[(int)GridDataInitialization.BoardTypes.TilledDirt];
                int uvStartIndex = (GridDataInitialization.getPosForMesh((int)pos.y) +
                    width *
                    GridDataInitialization.getPosForMesh((int)pos.x)) * 4;
                uv[uvStartIndex] = new float2(tex.pixelStartX,
                    tex.pixelStartY);
                uv[uvStartIndex + 1] = new float2(tex.pixelStartX,
                    tex.pixelEndY);
                uv[uvStartIndex + 2] = new float2(tex.pixelEndX,
                    tex.pixelEndY);
                uv[uvStartIndex + 3] = new float2(tex.pixelEndX,
                    tex.pixelStartY);
                tmp.SetUVs(0, uv);
                tmp.MarkModified();
            }
        }

        return jobHandle;
    }
}