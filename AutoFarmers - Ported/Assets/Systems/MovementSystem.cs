using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine;

public class MovementSystem : SystemBase
{
    public float deltaTime;
    public static NativeQueue<TagInfo> addRemoveTags;
    EntityQuery m_Group;
    
    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Translation), ComponentType.ReadOnly<MovementComponent>(),
            typeof(EntityInfo), typeof(MovingTag));
        addRemoveTags = new NativeQueue<TagInfo>(Allocator.Persistent);
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        if (addRemoveTags.IsCreated)
        {
            addRemoveTags.Dispose();
        }
        base.OnDestroy();
    }
    
    [BurstCompile]
    public struct MovementJob : IJobEntityBatch

    {
        public float deltaTime;
        public float2 rockPos;
        public NativeQueue<TagInfo>.ParallelWriter addRemoveTags;
        // chunk vars
        [ReadOnly] public ComponentTypeHandle<Rotation> RotationTypeHandle;
        [ReadOnly] public EntityTypeHandle EntityType;
        public ComponentTypeHandle<Translation> TranslationTypeHandle;
        public ComponentTypeHandle<MovementComponent> MovementTypeHandle;
        public ComponentTypeHandle<EntityInfo> EntityInfoTypeHandle;
        
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            float tolerance = 0.2f;

            var rotations = batchInChunk.GetNativeArray(RotationTypeHandle);
            var translations = batchInChunk.GetNativeArray(TranslationTypeHandle);
            var movements = batchInChunk.GetNativeArray(MovementTypeHandle);
            var intents = batchInChunk.GetNativeArray(EntityInfoTypeHandle);
            var entities = batchInChunk.GetNativeArray(EntityType);

            for (var i = 0; i < batchInChunk.Count; i++)
            {
                // Calculate DX and DZ (y represents up, therefore we won't be using that in this case).  
                float dx = movements[i].targetPos.x - translations[i].Value.x;
                float dz = movements[i].targetPos.y - translations[i].Value.z;

                if ((int) movements[i].middlePos.x != -1 && (int) movements[i].middlePos.y != -1)
                {
                    //Debug.Log("targeting middle" + actor.middlePos);
                    // our target is the middle pos before the target 
                    dx = movements[i].middlePos.x - translations[i].Value.x;
                    dz = movements[i].middlePos.y - translations[i].Value.z;
                }

                //the specs state that we want to move in the shortest distance first, therefore, we will perform a check to decide whether x or z is smaller
                //move from there. 
                bool moveXFirst;
                float2 currentPos = new float2(translations[i].Value.x, translations[i].Value.z);

                
                //Debug.Log("goal and target diff: " +  dx + " " + dz );
                if (Mathf.Abs(dx) <= Mathf.Abs(dz))
                {
                    moveXFirst = true;
                }
                else
                {
                    moveXFirst = false;
                }

                if (moveXFirst)
                {

                    if (Mathf.Abs(dx) > tolerance)
                    {
                        if (dx > 0)
                        {
                            //Debug.Log("moved towards dz");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x + movements[i].speed * deltaTime,
                                    translations[i].Value.y, translations[i].Value.z)
                            };
                        }
                        else
                        {
                            //Debug.Log("moved towards dz");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x - movements[i].speed * deltaTime,
                                    translations[i].Value.y, translations[i].Value.z)
                            };
                        }
                    }
                    else if (Mathf.Abs(dz) > tolerance)
                    {
                        if (dz < 0)
                        {
                            //Debug.Log("moved towards dx");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x, translations[i].Value.y,
                                    translations[i].Value.z - movements[i].speed * deltaTime)
                            };
                        }
                        else
                        {
                            //Debug.Log("moved towards dx");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x, translations[i].Value.y,
                                    translations[i].Value.z + movements[i].speed * deltaTime)
                            };
                        }
                    }
                    else
                    {
                        //Debug.Log("At destination and was I headed to a rock1?: " + actor.intent + " " + actor.targetPos.x + " " + actor.targetPos.y);
                        if ((int) movements[i].middlePos.x != -1 && (int) movements[i].middlePos.y != -1)
                        {
                            // we just hit the middle pos and so set things to go to target now
                            movements[i] = new MovementComponent
                            {
                                startPos = movements[i].startPos,
                                targetPos = movements[i].targetPos,
                                speed = movements[i].speed,
                                middlePos = new float2(-1, -1)
                            };
                        }
                        else if (intents[i].type == (int) Tiles.Rock || intents[i].type == (int) Tiles.Till ||
                                 intents[i].type == (int) Tiles.Plant || intents[i].type == (int) Tiles.Harvest ||
                                 intents[i].type == (int) Tiles.Store)
                        {
                            //Debug.Log("performing rock1 now");
                            // add performtask and remove moving tag
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 1, entity = entities[i], type = Tags.Moving});
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 0, entity = entities[i], type = Tags.PerformTask});


                        }
                        else
                        {
                            // add needstask and remove moving tag
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 1, entity = intents[i].specificEntity, type = Tags.Moving});
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 0, entity = intents[i].specificEntity, type = Tags.NeedsTask});

                        }

                    }

                }

                else
                {
                    if (Mathf.Abs(dz) > tolerance)
                    {
                        if (dz > 0)
                        {
                            //Debug.Log("moved towards dx");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x, translations[i].Value.y,
                                    translations[i].Value.z + movements[i].speed * deltaTime)
                            };
                        }
                        else
                        {
                            //Debug.Log("moved towards dx");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x, translations[i].Value.y,
                                    translations[i].Value.z - movements[i].speed * deltaTime)
                            };
                        }
                    }
                    else if (Mathf.Abs(dx) > tolerance)
                    {
                        if (dx > 0)
                        {
                            //Debug.Log("moved towards dz");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x + movements[i].speed * deltaTime,
                                    translations[i].Value.y, translations[i].Value.z)
                            };
                        }
                        else
                        {
                            //Debug.Log("moved towards dz");
                            translations[i] = new Translation
                            {
                                Value = new float3(translations[i].Value.x - movements[i].speed * deltaTime,
                                    translations[i].Value.y, translations[i].Value.z)
                            };

                        }

                    }
                    else
                    {
                        //Debug.Log("At destination and was I headed to a rock?: " + intents[i].type);

                        if ((int) movements[i].middlePos.x != -1 && (int) movements[i].middlePos.y != -1)
                        {
                            //Debug.Log("just got to the middle" + actor.middlePos);
                            // we just hit the middle pos and so set things to go to target now
                            movements[i] = new MovementComponent
                            {
                                startPos = movements[i].startPos,
                                targetPos = movements[i].targetPos,
                                speed = movements[i].speed,
                                middlePos = new float2(-1, -1)
                            };
                        }
                        else if (intents[i].type == (int) Tiles.Rock || intents[i].type == (int) Tiles.Till ||
                                 intents[i].type == (int) Tiles.Plant || intents[i].type == (int) Tiles.Harvest ||
                                 intents[i].type == (int) Tiles.Store)
                        {
                            //Debug.Log("performing rock now");  
                            // add perform task and remove moving tag
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 1, entity = entities[i], type = Tags.Moving});
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 0, entity = entities[i], type = Tags.PerformTask});


                        }
                        else
                        {
                            // add needs task and remove moving tag
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 1, entity = entities[i], type = Tags.Moving});
                            addRemoveTags.Enqueue(new TagInfo
                                {shouldRemove = 0, entity = entities[i], type = Tags.NeedsTask});
                        }

                    }
                }

            }
        }

    }

    public static float2 FindMiddlePos(float2 currentPos, float2 targetPos)
    {
        float2 middlePos = new float2();

        var dx = targetPos.x - currentPos.x;
        var dz = targetPos.y - currentPos.y;

        if (Mathf.Abs(dx) <= Mathf.Abs(dz))
        {
            middlePos = new float2(currentPos.x + dx, currentPos.y);
        }
        else
        {
            middlePos = new float2(currentPos.x, currentPos.y + dz);
        }

        return middlePos;
    }


    protected override void OnUpdate()
    {
        // chunk vars
        var translationType = GetComponentTypeHandle<Translation>();
        var rotationType = GetComponentTypeHandle<Rotation>(true);
        var movementType = GetComponentTypeHandle<MovementComponent>();
        var entityInfoType = GetComponentTypeHandle<EntityInfo>();
        var entities = GetEntityTypeHandle();
        
        // job
        var job = new MovementJob();
        job.deltaTime = Time.DeltaTime;
        job.addRemoveTags = addRemoveTags.AsParallelWriter();
        job.MovementTypeHandle = movementType;
        job.RotationTypeHandle = rotationType;
        job.TranslationTypeHandle = translationType;
        job.EntityInfoTypeHandle = entityInfoType;
        job.EntityType = entities;
        
        int batchesPerChunk = 4;
        this.Dependency = job.ScheduleParallel(m_Group, batchesPerChunk, this.Dependency);
    }
}