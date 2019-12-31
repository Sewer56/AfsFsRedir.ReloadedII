using System;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using Reloaded.Utils.AfsRedirector.Structs;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Reloaded.Utils.AfsRedirector
{
    public unsafe class Program : IMod
    {
        private IModLoader _modLoader;
        private AfsHook _afsHook;

        public void Start(IModLoaderV1 loader)
        {
            _modLoader = (IModLoader)loader;
            _modLoader.GetController<IReloadedHooks>().TryGetTarget(out var hooks);

            /* Your mod code starts here. */
            _afsHook = new AfsHook(NativeFunctions.GetInstance(hooks));
            _modLoader.ModLoading += OnModLoading;
            _modLoader.OnModLoaderInitialized += OnModLoaderInitialized;
        }

        private void OnModLoaderInitialized()
        {
            _modLoader.ModLoading -= OnModLoading;
            _modLoader.OnModLoaderInitialized -= OnModLoaderInitialized;
        }

        private void OnModLoading(IModV1 modInstance, IModConfigV1 modConfig) => _afsHook.OnModLoading(_modLoader.GetDirectoryForModId(modConfig.ModId));

        /* Mod loader actions. */
        public void Suspend() { }
        public void Resume() { }
        public void Unload() { }
        public bool CanUnload()  => false;
        public bool CanSuspend() => false;

        /* Automatically called by the mod loader when the mod is about to be unloaded. */
        public Action Disposing { get; }
    }
}
