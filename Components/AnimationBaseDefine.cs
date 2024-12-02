using System;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Entities;

using UniqueId = System.Byte;

namespace Latios.Kinemation.Graph
{
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct AnimationId
    {
        [FieldOffset(0)] public byte  TemplateId;
        [FieldOffset(2)] public sbyte RuntimeId;

        public bool IsValid => RuntimeId != -1;
        public static AnimationId Null => new() { TemplateId = 0, RuntimeId = -1 };

        public AnimationId(byte templateId)
        {
            TemplateId = templateId;
            RuntimeId  = -1;
        }

        public AnimationId(byte templateId, sbyte runtimeId)
        {
            TemplateId = templateId;
            RuntimeId  = runtimeId;
        }
    }

    #region BufferId

    [StructLayout(LayoutKind.Explicit, Size = 6)]
    public struct AnimationBufferId
    {
        [FieldOffset(0)] public UniqueId m_UniqueId;
        [FieldOffset(2)] public sbyte    m_BufferIndex;
        [FieldOffset(4)] public bool     m_HasInitialized;

        public bool IsValid => m_HasInitialized;
        public static AnimationBufferId Null => new() { m_UniqueId = 0, m_BufferIndex = -1, m_HasInitialized = false };

        public AnimationBufferId(UniqueId templateId)
        {
            m_UniqueId       = templateId;
            m_BufferIndex    = -1;
            m_HasInitialized = true;
        }

        public AnimationBufferId(UniqueId templateId, sbyte runtimeId)
        {
            m_UniqueId       = templateId;
            m_BufferIndex    = runtimeId;
            m_HasInitialized = true;
        }

        public UniqueId UniqueId => m_UniqueId;

        public static bool operator ==(AnimationBufferId lhs, AnimationBufferId rhs)
        {
            return lhs.m_UniqueId == rhs.m_UniqueId;
        }
        public static bool operator !=(AnimationBufferId lhs, AnimationBufferId rhs)
        {
            return lhs.m_UniqueId != rhs.m_UniqueId;
        }
        public override bool Equals(object obj)
        {
            AnimationBufferId id = (AnimationBufferId)obj;
            return m_UniqueId == id.m_UniqueId && m_BufferIndex == id.m_BufferIndex;
        }
        public override int GetHashCode()
        {
            return m_UniqueId.GetHashCode();
        }
    }
    public interface IElementWithUniqueId
    {
        public UniqueId Id { get; }
    }

    public interface IMatcher<T> where T : unmanaged
    {
        public bool Matches(in T candidate);
    }

    public static class AnimationBufferIdExtensions
    {
        public static T GetByBufferId<T>(this DynamicBuffer<T> elements, ref AnimationBufferId id) where T : unmanaged, IBufferElementData,
        IElementWithUniqueId
        {
            if (!id.IsValid)
                return default(T);

            if (id.m_BufferIndex >= 0 && elements.Length > id.m_BufferIndex && elements[id.m_BufferIndex].Id == id.UniqueId)
            {
                return elements.ElementAt(id.m_BufferIndex);
            }

            Assert.IsTrue(elements.Length <= sbyte.MaxValue);
            for (sbyte index = 0; index < (sbyte)elements.Length; index++)
            {
                if (elements[index].Id == id.UniqueId)
                {
                    id.m_BufferIndex = index;
                    return elements.ElementAt(id.m_BufferIndex);
                }
            }

            return default(T);
        }

        public static sbyte GetIndexByBufferId<T>(this DynamicBuffer<T> elements, ref AnimationBufferId id) where T : unmanaged, IBufferElementData,
        IElementWithUniqueId
        {
            if (!id.IsValid)
                return -1;

            if (id.m_BufferIndex >= 0 && elements.Length > id.m_BufferIndex && elements[id.m_BufferIndex].Id == id.UniqueId)
            {
                return id.m_BufferIndex;
            }

            Assert.IsTrue(elements.Length <= sbyte.MaxValue);
            for (sbyte index = 0; index < (sbyte)elements.Length; index++)
            {
                if (elements[index].Id == id.UniqueId)
                {
                    id.m_BufferIndex = index;
                    return id.m_BufferIndex;
                }
            }

            return -1;
        }

        public static sbyte GetIndexByBufferId<T, U>(this DynamicBuffer<T> elements, ref AnimationBufferId id, U matcher)
            where T : unmanaged, IBufferElementData, IElementWithUniqueId
            where U : unmanaged, IMatcher<T>
        {
            if (!id.IsValid)
                return -1;

            if (id.m_BufferIndex >= 0 && elements.Length > id.m_BufferIndex && elements[id.m_BufferIndex].Id == id.UniqueId)
            {
                return id.m_BufferIndex;
            }

            Assert.IsTrue(elements.Length <= sbyte.MaxValue);
            for (sbyte index = 0; index < (sbyte)elements.Length; index++)
            {
                if (elements[index].Id == id.UniqueId && matcher.Matches(elements[index]))
                {
                    id.m_BufferIndex = index;
                    return id.m_BufferIndex;
                }
            }

            return -1;
        }
    }

    #endregion
}

