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
    private EntityQuery m_RockQuery;
    private EntityCommandBufferSystem ecbs;
    private static NativeArray<float2> tillChanges;
    private NativeArray<int> hasChanged; // variable to let me know when something changes the tillChanges array

    protected override void OnCreate()
    {
        ecbs = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        m_RockQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<RockTag>(), typeof(Translation) },

        });

        // this is basically just a bool that gets passed by reference from
        // being in an array to the job system
        hasChanged = new NativeArray<int>(1, Allocator.Persistent);
        hasChanged[0] = 0;

    }

    public static void InitializeTillSystem(int maxFarmers)
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }
        else
        {
            tillChanges = new NativeArray<float2>(maxFarmers, Allocator.Persistent);

            for (int i = 0; i < maxFarmers; i++)
            {
                tillChanges[i] = new float2(-1, -1);
            }
        }

    }

    protected override void OnDestroy()
    {
        if (tillChanges.IsCreated)
        {
            tillChanges.Dispose();
        }


        if (hasChanged.IsCreated)
        {
            hasChanged.Dispose();
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
        public NativeArray<float2> changes; // has all changes that max farmers could have made to their tiles
                                            // during a job
        public NativeArray<int> hasChanged; // only size one, but we pass by ref easily this way

        // var's specific to planting:
        [ReadOnly] public Entity plantEntity;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation translation, ref Rotation rotation, ref EntityInfo entityInfo)
        {

            if (entityInfo.type == (int)Tiles.Rock)
            {
                //Debug.Log("destroying rock");
                ecb.DestroyEntity(entityInfo.specificEntity.Index, entityInfo.specificEntity);
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                //ecb.RemoveComponent(index, entity, typeof(EntityInfo));
                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Till)
            {
                float tillBlockHeight = 0.25f;
                EntityInfo harvestInfo = new EntityInfo { type = (int)Tiles.Till };
                if (
                grid.TryAdd(GridData.ConvertToHash((int)translation.Value.x, (int)translation.Value.z),
                harvestInfo))
                {
                    float3 pos = new float3((int)translation.Value.x, tillBlockHeight, (int)translation.Value.z);

                    changes[index] = new float2((int)pos.x, (int)pos.z);
                    // 1 is true : set so that we only do the expensive uv changes across all entities if we need to
                    // FIX: SHOULD BE AN INTERLOCKED ADD HERE FOR SAFETY
                    hasChanged[0]++;
                }

                ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
            }
            else if (entityInfo.type == (int)Tiles.Plant)
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
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                    //Debug.Log("added grid plant");
                }
                else
                {
                    //Debug.Log("did not add to plant");
                    ecb.AddComponent(index, entity, typeof(NeedsTaskTag));
                    ecb.RemoveComponent(index, entity, typeof(PerformTaskTag));
                }
            }

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        GridData data = GridData.GetInstance();

        var job = new PerformTaskSystemJob();
        job.IsRockType = GetComponentDataFromEntity<RockTag>(true);
        job.ecb = ecbs.CreateCommandBuffer().ToConcurrent();
        job.changes = tillChanges;
        job.hasChanged = this.hasChanged;
        job.grid = data.gridStatus.AsParallelWriter();
        job.plantEntity = GridDataInitialization.plantEntity;
        JobHandle jobHandle = job.Schedule(this, inputDependencies);
        jobHandle.Complete();

        // we have to have a sync point here since we're about to change all uv's for the frame
        // This happens on main thread since uv's are Vector2 types and can't be changed inside of the job
        if (hasChanged[0] != 0)
        {
            for (int i = 0; i < GridDataInitialization.MaxFarmers; i++)
            {
                float2 pos = tillChanges[i];
                if ((int)pos.x != -1 && (int)pos.y != -1)
                {
                    // set the uv's on the mesh
                    // NOTE: set pos to be a specific number for testing
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
                    // then set the pos back to -1's
                    tillChanges[i] = new float2(-1, -1);
                    tmp.SetUVs(0, uv);
                    tmp.MarkModified();
                }
                hasChanged[0] = 0; // nothing has been changed if it's a zero
            }


        }

        return jobHandle;
    }
}