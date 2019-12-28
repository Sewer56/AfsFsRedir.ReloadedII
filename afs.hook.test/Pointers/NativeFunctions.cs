using System;
using Reloaded.Hooks.Definitions;
using static Afs.Hook.Test.Native.Native;

namespace Afs.Hook.Test.Pointers
{
    public struct NativeFunctions
    {
        private static bool _instanceMade;
        private static NativeFunctions _instance;

        public IFunction<NtCreateFile> NtCreateFile;
        public IFunction<NtReadFile> NtReadFile;
        public IFunction<NtClose> NtClose;

        public NativeFunctions(IntPtr ntCreateFile, IntPtr ntReadFile, IntPtr ntClose, IReloadedHooks hooks)
        {
            NtCreateFile = hooks.CreateFunction<NtCreateFile>((long) ntCreateFile);
            NtReadFile = hooks.CreateFunction<NtReadFile>((long) ntReadFile);
            NtClose = hooks.CreateFunction<NtClose>((long) ntClose);
        }

        public static NativeFunctions GetInstance(IReloadedHooks hooks)
        {
            if (_instanceMade)
                return _instance;

            var ntdllHandle = LoadLibraryW("ntdll");
            var ntCreateFilePointer = GetProcAddress(ntdllHandle, "NtCreateFile");
            var ntReadFilePointer = GetProcAddress(ntdllHandle, "NtReadFile");
            var ntClosePointer = GetProcAddress(ntdllHandle, "NtClose");
            _instance = new NativeFunctions(ntCreateFilePointer, ntReadFilePointer, ntClosePointer, hooks);
            _instanceMade = true;

            return _instance;
        }
    }
}
