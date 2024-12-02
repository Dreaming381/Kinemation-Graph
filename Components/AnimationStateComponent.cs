using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Kinemation.Graph
{
    public struct AnimationStateComponent : IBufferElementData, IElementWithUniqueId
    {
        public byte  m_StateIndex;
        public float m_Weight;

        public float m_Time;

        public bool m_NeedInit;

        public byte Id { get => m_StateIndex; }
        public bool IsValid => m_Weight >= math.EPSILON;
    }

    public struct AnimationCurrentStateComponent : IComponentData
    {
        public AnimationBufferId m_StateId;
        public bool IsValid => m_StateId.IsValid;
        public static AnimationCurrentStateComponent Null => new() { m_StateId = AnimationBufferId.Null };

        public AnimationCurrentStateComponent(AnimationBufferId stateId)
        {
            m_StateId = stateId;
        }
    }

    public struct AnimationStateTransitionComponent : IComponentData
    {
        public AnimationBufferId m_StateId;
        public float             m_TransitionDuration;
        public static AnimationStateTransitionComponent Null => new() { m_StateId = AnimationBufferId.Null };
        public bool IsValid => m_StateId.IsValid;

        public bool HasEnded(in AnimationStateComponent animationState)
        {
            Assert.AreEqual(animationState.m_StateIndex, m_StateId.UniqueId);
            return animationState.m_Time > m_TransitionDuration;
        }
    }

    public struct AnimationStateTransitionRequestComponent : IComponentData
    {
        public AnimationBufferId m_NextStateId;
        public float             m_TransitionDuration;
        public bool IsValid => m_NextStateId.IsValid;

        public static AnimationStateTransitionRequestComponent Null => new AnimationStateTransitionRequestComponent() {
            m_NextStateId = AnimationBufferId.Null
        };
    }

    public struct AnimationPreserveStateComponent : IComponentData
    {
        public AnimationBufferId m_StateId;
        public static AnimationPreserveStateComponent Null => new() { m_StateId = AnimationBufferId.Null };
        public bool IsValid => m_StateId.IsValid;
    }
}

