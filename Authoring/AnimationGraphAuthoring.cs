using System;
using Latios.Authoring;
using Latios.Kinemation;
using Latios.Kinemation.Authoring;
using Latios.Kinemation.Graph.Asset;
using Latios.Kinemation.Graph.Event;
using Latios.Kinemation.Graph.Event.Authoring;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Latios.Kinemation.Graph.Authoring
{
    public class AnimationGraphAuthoring : MonoBehaviour
    {
        public AnimationGraphAsset m_AnimationGraphAsset;
        public RootMotionMode      m_RootMotionMode;
        public bool                m_EnableEvents = true;
    }

    public struct AnimationGraphSmartBakeItem : ISmartBakeItem<AnimationGraphAuthoring>
    {
        //Blob
        private SmartBlobberHandle<AnimationGraphBlob>  m_AnimationGraphBlobHandle;
        private SmartBlobberHandle<SkeletonClipSetBlob> m_ClipsBlobHandle;

        public bool Bake(AnimationGraphAuthoring authoring, IBaker baker)
        {
            var animator = baker.GetComponent<Animator>();
            if (animator == null || authoring.m_AnimationGraphAsset == null)
                return false;

            //Animation clips
            {
                var clips         = authoring.m_AnimationGraphAsset.GetClipConfigs(Allocator.Temp);
                m_ClipsBlobHandle = baker.RequestCreateBlobAsset(animator, clips.AsArray());
            }

            //AnimationGraph
            m_AnimationGraphBlobHandle = baker.RequestCreateBlobAsset(authoring.m_AnimationGraphAsset);

            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);

            AnimationGraphConversionUtils.AddAnimationGraphComponents(baker, entity, authoring.m_AnimationGraphAsset);

            if (authoring.m_EnableEvents && authoring.m_AnimationGraphAsset.HasEventInClips)
                baker.AddBuffer<RaisedAnimationEvent>(entity);

            AnimationGraphConversionUtils.AddRootMotionComponents(baker, entity, authoring.m_RootMotionMode);
            return true;
        }

        public void PostProcessBlobRequests(EntityManager entityManager, Entity entity)
        {
            var stateGraphBlob = m_AnimationGraphBlobHandle.Resolve(entityManager);
            var clipsBlob      = m_ClipsBlobHandle.Resolve(entityManager);

            AnimationGraphConversionUtils.SetAnimationGraphComponents(entityManager, entity, stateGraphBlob, clipsBlob);
        }
    }

    public static class AnimationGraphConversionUtils
    {
        public static void AddAnimationGraphComponents(IBaker baker, Entity entity,
                                                       AnimationGraphAsset animationGraphAsset)
        {
            //graph data
            baker.AddComponent(entity, new AnimationGraphComponent());
            var machineEntityBuffer = baker.AddBuffer<AnimationMachineEntityBuffer>(entity);
            var clipSamplers        = baker.AddBuffer<AnimationClipSampler>(entity);
            clipSamplers.Capacity   = 10;

            AddAnimationNodeDatas(baker, entity);

            //state machine data
            Assert.IsTrue(animationGraphAsset.m_StateMachines.Count <= byte.MaxValue);
            for (byte index = 0; index < (byte)animationGraphAsset.m_StateMachines.Count; index++)
            {
                var stateMachine = new AnimationStateMachineComponent
                {
                    m_AnimationGraphBlob = default,
                    m_StateMachineIndex  = index,
                    m_Weight             = 0.0f,
                    m_CurrentState       = StateMachineStateRef.Null,
 #if UNITY_EDITOR || DEBUG
                    m_PreviousState = StateMachineStateRef.Null
 #endif
                };

                var fsmEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None);
                baker.AddComponent(fsmEntity, stateMachine);
                AddAnimationStateComponents(baker, fsmEntity);
                AddAnimationNodeDatas(baker, fsmEntity);
                AddAnimationParameter(baker, fsmEntity);

                baker.AddComponent(fsmEntity, new AnimationGraphRefComponent { m_GraphEntity = entity });

                machineEntityBuffer.Add(new AnimationMachineEntityBuffer { m_StateMachineEntity = fsmEntity, m_StateMachineIndex = index });
            }

            //Parameters
            {
                AddAnimationParameter(baker, entity);
            }
        }

        public static void SetAnimationGraphComponents(EntityManager dstManager, Entity entity,
                                                       BlobAssetReference<AnimationGraphBlob> aniamtionGraphBlob,
                                                       BlobAssetReference<SkeletonClipSetBlob> clipsBlob)
        {
            //graph data
            {
                var animationGraph = new AnimationGraphComponent
                {
                    m_AnimationGraphBlob = aniamtionGraphBlob,
                    m_ClipsBlob          = clipsBlob,
                };

                dstManager.SetComponentData(entity, animationGraph);
                SetAnimationNodeDatas(dstManager, entity, ref aniamtionGraphBlob.Value.Nodes);
            }

            //state machine data
            var machineEntityBuffer = dstManager.GetBuffer<AnimationMachineEntityBuffer>(entity).AsNativeArray();
            for (byte index = 0; index < (byte)machineEntityBuffer.Length; index++)
            {
                var stateMachine = new AnimationStateMachineComponent
                {
                    m_AnimationGraphBlob = aniamtionGraphBlob,
                    m_StateMachineIndex  = index,
                    m_Weight             = 0.0f,
                    m_CurrentState       = StateMachineStateRef.Null,
#if UNITY_EDITOR || DEBUG
                    m_PreviousState = StateMachineStateRef.Null
#endif
                };

                var fsmEntity = machineEntityBuffer[index].m_StateMachineEntity;
                SetAnimationParameter(dstManager, fsmEntity, aniamtionGraphBlob);
            }

            //Parameters
            {
                SetAnimationParameter(dstManager, entity, aniamtionGraphBlob);
            }
        }

        public static void AddAnimationStateComponents(IBaker baker, Entity entity)
        {
            baker.AddBuffer<AnimationStateComponent>(entity);
            baker.AddComponent(entity, AnimationStateTransitionComponent.Null);
            baker.AddComponent(entity, AnimationStateTransitionRequestComponent.Null);
            baker.AddComponent(entity, AnimationCurrentStateComponent.Null);
            baker.AddComponent(entity, AnimationPreserveStateComponent.Null);
        }

        public static void AddAnimationNodeDatas(IBaker baker, Entity entity)
        {
            AnimationNodeDefines.AddAnimationNodeBufferInternal(baker, entity);
            //the state graph node will be added only when the state is created
        }

        public static void SetAnimationNodeDatas(EntityManager dstManager, Entity entity, ref AnimationNodeContextBlob nodeContextBlob)
        {
            //add animation graph node directly
            AnimationNodeDefines.AddAnimationNodeDatas(dstManager, entity, ref nodeContextBlob);
        }

        public static void AddRootMotionComponents(IBaker baker, Entity entity, RootMotionMode mode)
        {
            switch (mode)
            {
                case RootMotionMode.Disabled:
                    break;
                case RootMotionMode.EnabledAuto:
                    baker.AddComponent(entity, new RootDeltaTranslation());
                    baker.AddComponent(entity, new RootDeltaRotation());
                    baker.AddComponent(entity, new ApplyRootMotionToEntity());

                    break;
                case RootMotionMode.EnabledManual:
                    baker.AddComponent(entity, new RootDeltaTranslation());
                    baker.AddComponent(entity, new RootDeltaRotation());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void AddAnimationParameter(IBaker baker, Entity entity)
        {
            //TODO Randall 动画图和动画状态机需要在这里做出区分，状态机仅保存自己需要的数据
            baker.AddBuffer<BoolParameter>( entity);
            baker.AddBuffer<IntParameter>(  entity);
            baker.AddBuffer<FloatParameter>(entity);
        }

        public static void SetAnimationParameter(EntityManager dstManager, Entity entity, BlobAssetReference<AnimationGraphBlob> aniamtionGraphBlob)
        {
            //TODO Randall 动画图和动画状态机需要在这里做出区分，状态机仅保存自己需要的数据
            dstManager.AddBuffer<BoolParameter>( entity);
            dstManager.AddBuffer<IntParameter>(  entity);
            dstManager.AddBuffer<FloatParameter>(entity);

            ref var parameters = ref aniamtionGraphBlob.Value.Parameters;

            var boolParameters = dstManager.GetBuffer<BoolParameter>(entity);
            foreach (var item in parameters.BoolParameters.ToArray())
                boolParameters.Add(new BoolParameter(item));

            var intParameters = dstManager.GetBuffer<IntParameter>(entity);
            foreach (var item in parameters.IntParameters.ToArray())
                intParameters.Add(new IntParameter(item));

            var floatParameters = dstManager.GetBuffer<FloatParameter>(entity);
            foreach (var item in parameters.FloatParameters.ToArray())
                floatParameters.Add(new FloatParameter(item));
        }
    }
}

