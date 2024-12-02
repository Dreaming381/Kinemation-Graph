using Latios.Kinemation;
using Latios.Kinemation.Graph.Asset;
using Unity.Entities;

namespace Latios.Kinemation.Graph
{
    public struct AnimationGraphRefComponent : IComponentData
    {
        public Entity m_GraphEntity;
    }

    public struct AnimationMachineEntityBuffer : IBufferElementData, IElementWithUniqueId
    {
        public byte   m_StateMachineIndex;
        public Entity m_StateMachineEntity;

        public byte Id => m_StateMachineIndex;
    }

    public struct AnimationGraphComponent : IComponentData
    {
        public BlobAssetReference<AnimationGraphBlob>  m_AnimationGraphBlob;
        public BlobAssetReference<SkeletonClipSetBlob> m_ClipsBlob;
        //public BlobAssetReference<ClipEventsBlob>      m_ClipEventsBlob;
    }
}

