using System;
using System.Diagnostics;
using Afs.Hook.Test.Pointers;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Afs.Hook.Test
{
    public unsafe class Program : IMod, IExports
    {
        private IModLoader _modLoader;
        private AfsHook _afsHook;

        public void Start(IModLoaderV1 loader)
        {
            _modLoader = (IModLoader)loader;
            _modLoader.GetController<IReloadedHooks>().TryGetTarget(out var hooks);

            /* Your mod code starts here. */
            _afsHook = new AfsHook(NativeFunctions.GetInstance(hooks), hooks);
        }

        /* Mod loader actions. */
        public void Suspend()
        {

        }

        public void Resume()
        {

        }

        public void Unload()
        {

        }

        /*  If CanSuspend == false, suspend and resume button are disabled in Launcher and Suspend()/Resume() will never be called.
            If CanUnload == false, unload button is disabled in Launcher and Unload() will never be called.
        */
        public bool CanUnload()  => false;
        public bool CanSuspend() => false;

        /* Automatically called by the mod loader when the mod is about to be unloaded. */
        public Action Disposing { get; }

        /* Contains the Types you would like to share with other mods.
           If you do not want to share any types, please remove this method and the
           IExports interface.
        
           Inter Mod Communication: https://github.com/Reloaded-Project/Reloaded-II/blob/master/Docs/InterModCommunication.md
        */
        public Type[] GetTypes() => new Type[0];
    }
}
