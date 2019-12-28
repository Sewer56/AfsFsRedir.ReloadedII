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

        public NativeFunctions(IntPtr ntCreateFile, IntPtr ntReadFile, IReloadedHooks hooks)
        {
            NtCreateFile = hooks.CreateFunction<NtCreateFile>((long) ntCreateFile);
            NtReadFile = hooks.CreateFunction<NtReadFile>((long) ntReadFile);
        }

        public static NativeFunctions GetInstance(IReloadedHooks hooks)
        {
            if (_instanceMade)
                return _instance;

            var ntdllHandle = LoadLibraryW("ntdll");
            var ntCreateFilePointer = GetProcAddress(ntdllHandle, "NtCreateFile");
            var ntReadFilePointer = GetProcAddress(ntdllHandle, "NtReadFile");
            _instance = new NativeFunctions(ntCreateFilePointer, ntReadFilePointer, hooks);
            _instanceMade = true;

            return _instance;
        }
    }
}
