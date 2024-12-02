using Unity.Entities;

namespace Latios.Kinemation.Graph
{
    public static class KinemationGraphBootstrap
    {
        public static void InstallKinemationGraph(World world)
        {
            BootstrapTools.InjectSystem(TypeManager.GetSystemTypeIndex<KinemationGraphRootSuperSystem>(), world);
        }
    }

#if LATIOS_TRANSFORMS_UNITY
    [UpdateInGroup(typeof(Unity.Transforms.TransformSystemGroup), OrderFirst = true)]
#else
    [UpdateInGroup(typeof(Latios.Transforms.Systems.PreTransformSuperSystem))]
#endif
    [DisableAutoCreation]
    public partial class KinemationGraphRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }
}

