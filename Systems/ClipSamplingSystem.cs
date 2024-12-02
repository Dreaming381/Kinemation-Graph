using Latios.Kinemation;
using Latios.Kinemation.Graph.Event;
using Latios.Transforms;
using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Kinemation.Graph
{
    [UpdateInGroup(typeof(KinemationGraphRootSuperSystem))]
    public partial class ClipSamplingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var dependency = Dependency;

            //1. sample optimize skeleton
            dependency = Entities.WithAll<AnimationGraphComponent>().ForEach((
                                                                                 OptimizedSkeletonAspect skeleton,
                                                                                 in AnimationGraphComponent animationGraph,
                                                                                 in DynamicBuffer<AnimationClipSampler> samplers) =>
            {
                ref var clipBlobs = ref animationGraph.m_ClipsBlob.Value;

                var activeSamplerCount = 0;

                Assert.IsTrue(samplers.Length < byte.MaxValue);
                for (byte i = 0; i < samplers.Length; i++)
                {
                    var sampler = samplers[i];

                    if (sampler.m_Weight > math.EPSILON)
                    {
                        ref var clipBlob   = ref clipBlobs.clips[sampler.m_ClipIndex];
                        var     sampleTime = sampler.m_Loop ? clipBlob.LoopToClipTime(sampler.m_Time) : sampler.m_Time;
                        clipBlob.SamplePose(ref skeleton, sampler.m_Weight, sampleTime);

                        activeSamplerCount++;
                    }
                }

                if (activeSamplerCount > 0)
                {
                    skeleton.EndSamplingAndSync();
                }
            }).ScheduleParallel(dependency);

            //2. raise animation events
            dependency = Entities.WithAll<AnimationGraphComponent>().ForEach((
                                                                                 ref DynamicBuffer<RaisedAnimationEvent> raisedAnimationEvents,
                                                                                 in AnimationGraphComponent animationGraph,
                                                                                 in DynamicBuffer<AnimationClipSampler> samplers) =>
            {
                raisedAnimationEvents.Clear();

                ref var clips = ref animationGraph.m_ClipsBlob.Value.clips;
                for (var samplerIndex = 0; samplerIndex < samplers.Length; samplerIndex++)
                {
                    var sampler = samplers[samplerIndex];
                    if (sampler.m_Weight <= Mathf.Epsilon)
                        continue;

                    var clipIndex           = sampler.m_ClipIndex;
                    var previousSamplerTime = sampler.m_PreviousTime;
                    var currentSamplerTime  = sampler.m_Time;

                    ref var events = ref clips[clipIndex].events;
                    for (short i = 0; i < events.times.Length; i++)
                    {
                        var  eClipTime = events.times[i];
                        bool shouldRaiseEvent;

                        if (previousSamplerTime > currentSamplerTime)
                        {
                            //this mean we looped the clip
                            Assert.IsTrue(sampler.m_Loop);
                            shouldRaiseEvent = (eClipTime > previousSamplerTime && eClipTime <= sampler.m_TotalTime) || (eClipTime > 0 && eClipTime <= currentSamplerTime);
                        }
                        else
                        {
                            shouldRaiseEvent = eClipTime > previousSamplerTime && eClipTime <= currentSamplerTime;
                        }

                        if (shouldRaiseEvent)
                        {
                            raisedAnimationEvents.Add(new RaisedAnimationEvent()
                            {
                                ClipWeight      = sampler.m_Weight,
                                FunctionEvent   = events.names[i],  //e.FunctionEvent,
                                intParameter    = events.parameters[i],  //e.intParameter,
                                floatParameter  = math.asfloat(events.parameters[i]),  //e.floatParameter,
                                stringParameter = events.names[i],  //e.stringParameter
                            });
                        }
                    }
                }
            }).ScheduleParallel(dependency);

            //3. send animation event to outside
            dependency = Entities.ForEach((in DynamicBuffer<RaisedAnimationEvent> raisedEvents) =>
            {
            }).ScheduleParallel(dependency);

            //4. Sample root delta (need to separate, since we support the animator without root motion)
            dependency = Entities.WithAll<AnimationGraphComponent>().ForEach((
                                                                                 ref RootDeltaTranslation rootDeltaTranslation,
                                                                                 ref RootDeltaRotation rootDeltaRotation,
                                                                                 in AnimationGraphComponent animationGraph,
                                                                                 in DynamicBuffer<AnimationClipSampler> samplers) =>
            {
                rootDeltaTranslation.Value = 0;
                rootDeltaRotation.Value    = quaternion.identity;

                ref var clipBlobs      = ref animationGraph.m_ClipsBlob.Value;
                bool    needInit       = true;
                var     rootTf         = new TransformQvvs();
                var     previousRootTf = new TransformQvvs();

                Assert.IsTrue(samplers.Length < byte.MaxValue);
                for (byte i = 0; i < samplers.Length; i++)
                {
                    var sampler = samplers[i];
                    if (sampler.m_Weight <= math.EPSILON)
                        continue;

                    ref var clipBlob           = ref clipBlobs.clips[sampler.m_ClipIndex];
                    var     currentSampleTime  = sampler.m_Loop ? clipBlob.LoopToClipTime(sampler.m_Time) : sampler.m_Time;
                    var     previousSampleTime = sampler.m_Loop ? clipBlob.LoopToClipTime(sampler.m_PreviousTime) : sampler.m_PreviousTime;
                    if (currentSampleTime == previousSampleTime)
                        continue;

                    if (needInit)
                    {
                        rootTf         = SampleWeightedFirstIndex(0, ref clipBlob, currentSampleTime, sampler.m_Weight);
                        previousRootTf = SampleWeightedFirstIndex(0, ref clipBlob, previousSampleTime, sampler.m_Weight);
                        needInit       = false;
                    }
                    else
                    {
                        SampleWeightedNIndex(ref rootTf,         0, ref clipBlob, currentSampleTime,  sampler.m_Weight);
                        SampleWeightedNIndex(ref previousRootTf, 0, ref clipBlob, previousSampleTime, sampler.m_Weight);
                    }
                }

                rootDeltaTranslation.Value = rootTf.position - previousRootTf.position;
                var inv                    = math.inverse(rootTf.rotation);
                rootDeltaRotation.Value    = math.normalizesafe(math.mul(inv, previousRootTf.rotation));
            }).ScheduleParallel(dependency);

            //5. Apply root motion to entity
            dependency = Entities.WithAll<AnimationGraphComponent, ApplyRootMotionToEntity, SkeletonRootTag>().ForEach((
                                                                                                                           Latios.Transforms.Abstract.
                                                                                                                           LocalTransformQvvsReadWriteAspect transform,
                                                                                                                           in RootDeltaTranslation rootDeltaTranslation,
                                                                                                                           in RootDeltaRotation rootDeltaRotation) =>
            {
                var t                     = transform.localTransform;
                t.position               += rootDeltaTranslation.Value;
                t.rotation                = math.mul(rootDeltaRotation.Value, t.rotation);
                transform.localTransform  = t;
            }).ScheduleParallel(dependency);

            Dependency = dependency;
        }

        private static TransformQvvs SampleWeightedFirstIndex(int boneIndex, ref SkeletonClip clip, float time, float weight)
        {
            var bone       = clip.SampleBone(boneIndex, time);
            bone.position *= weight;
            var rot        = bone.rotation;
            rot.value     *= weight;
            bone.rotation  = rot;
            bone.scale    *= weight;
            return bone;
        }

        private static void SampleWeightedNIndex(ref TransformQvvs bone, int boneIndex, ref SkeletonClip clip, float time, float weight)
        {
            var otherBone  = clip.SampleBone(boneIndex, time);
            bone.position += otherBone.position * weight;

            //blends rotation. Negates opposing quaternions to be sure to choose the shortest path
            var otherRot = otherBone.rotation;
            var dot      = math.dot(otherRot, bone.rotation);
            if (dot < 0)
            {
                otherRot.value = -otherRot.value;
            }

            var rot        = bone.rotation;
            rot.value     += otherRot.value * weight;
            bone.rotation  = rot;

            bone.scale += otherBone.scale * weight;
        }
    }
}

