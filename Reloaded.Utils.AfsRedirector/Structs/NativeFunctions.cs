using System;
using Reloaded.Hooks.Definitions;
using static Reloaded.Utils.AfsRedirector.Native.Native;

namespace Reloaded.Utils.AfsRedirector.Structs;

public struct NativeFunctions
{
    private static bool _instanceMade;
    private static NativeFunctions _instance;

    public IFunction<NtCreateFile> NtCreateFile;
    public IFunction<NtReadFile> NtReadFile;
    public IFunction<NtSetInformationFile> SetFilePointer;
    public IFunction<NtQueryInformationFile> GetFileSize;

    public NativeFunctions(IntPtr ntCreateFile, IntPtr ntReadFile, IntPtr ntSetInformationFile, IntPtr ntQueryInformationFile, IReloadedHooks hooks)
    {
        NtCreateFile = hooks.CreateFunction<NtCreateFile>((long) ntCreateFile);
        NtReadFile = hooks.CreateFunction<NtReadFile>((long) ntReadFile);
        SetFilePointer = hooks.CreateFunction<NtSetInformationFile>((long) ntSetInformationFile);
        GetFileSize = hooks.CreateFunction<NtQueryInformationFile>((long) ntQueryInformationFile);
    }

    public static NativeFunctions GetInstance(IReloadedHooks hooks)
    {
        if (_instanceMade)
            return _instance;

        var ntdllHandle    = LoadLibraryW("ntdll");
        var ntCreateFilePointer = GetProcAddress(ntdllHandle, "NtCreateFile");
        var ntReadFilePointer = GetProcAddress(ntdllHandle, "NtReadFile");
        var setFilePointer = GetProcAddress(ntdllHandle, "NtSetInformationFile");
        var getFileSize = GetProcAddress(ntdllHandle, "NtQueryInformationFile");

        _instance = new(ntCreateFilePointer, ntReadFilePointer, setFilePointer, getFileSize, hooks);
        _instanceMade = true;

        return _instance;
    }
}