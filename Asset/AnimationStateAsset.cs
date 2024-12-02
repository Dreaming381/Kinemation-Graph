using System;
using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Latios.Kinemation.Graph.Asset
{
    [Serializable]
    public class AnimationStateAsset : BaseGraph, IGetAnimationGlobalId
    {
        public string                     m_StateName;
        public AnimationGraphAsset        m_OwnerGraph;
        public AnimationStateMachineAsset m_OwnerStateMachine;

        public List<AnimationClipNodeAsset> m_Nodes
        {
            get
            {
                return nodes.OfType<AnimationClipNodeAsset>().ToList();
            }
        }

        public IEnumerable<AnimationClip> Clips => m_Nodes.SelectMany(node => node.Clips);
        public int ClipCount => m_Nodes.Sum(node => node.ClipCount);

        public byte GetAnimationNodeGlobalId()
        {
            return m_OwnerStateMachine.GetAnimationNodeGlobalId();
        }
    }

    public struct AnimationStateBlob
    {
        public FixedString64Bytes                      Name;
        public AnimationNodeContextBlob                Nodes;
        public BlobArray<AnimationStateTransitionBlob> Transitions;
    }

    public struct AnimationStateConvertContext
    {
        public FixedString64Bytes                                 Name;
        public AnimationNodeContext                               Nodes;
        public UnsafeList<AnimationStateTransitionConvertContext> Transitions;
    }
}

