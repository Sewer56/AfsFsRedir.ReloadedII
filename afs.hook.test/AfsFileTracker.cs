using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Afs.Hook.Test.Structs;
using Reloaded.Hooks.Definitions;
using FileInfo = Afs.Hook.Test.Structs.FileInfo;

namespace Afs.Hook.Test
{
    /// <summary>
    /// Monitors calls to NtCreateFile, keeping track of all 
    /// </summary>
    public unsafe class AfsFileTracker
    {
        /// <summary>
        /// Maps file handles to file paths.
        /// </summary>
        private ConcurrentDictionary<IntPtr, FileInfo> _handleToInfoMap = new ConcurrentDictionary<IntPtr, FileInfo>();
        private IHook<Native.Native.NtCreateFile> _createFileHook;
        private IHook<Native.Native.NtReadFile> _readFileHook;
        private IHook<Native.Native.NtSetInformationFile> _setFilePointerHook;

        private object _createLock = new object();
        private object _setFilePointerLock = new object();
        private object _readLock = new object();

        public AfsFileTracker(NativeFunctions functions)
        {
            _createFileHook = functions.NtCreateFile.Hook(NtCreateFileImpl).Activate();
            _readFileHook = functions.NtReadFile.Hook(NtReadFileImpl).Activate();
            _setFilePointerHook = functions.SetFilePointer.Hook(SetInformationFileImpl).Activate();


            // TODO: Hook NtClose
            // Problem: Native->Managed Transition hits NtClose in .NET Core, so our hook code is never hit.
            // Problem: NtClose needs synchronization.
            // Solution: Write custom ASM to solve the problem, see NtClose branch.
        }

        private int SetInformationFileImpl(IntPtr hfile, out Native.Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.Native.FileInformationClass fileInformationClass)
        {
            lock (_setFilePointerLock)
            {
                if (_handleToInfoMap.ContainsKey(hfile) && fileInformationClass == Native.Native.FileInformationClass.FilePositionInformation)
                {
                    var pointer = *(long*) fileInformation;
                    _handleToInfoMap[hfile].FilePointer = pointer;
                }

                return _setFilePointerHook.OriginalFunction(hfile, out ioStatusBlock, fileInformation, length, fileInformationClass);
            }
        }

        private unsafe int NtReadFileImpl(IntPtr handle, IntPtr hEvent, ref IntPtr apcRoutine, ref IntPtr apcContext, ref Native.Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, long* byteOffset, IntPtr key)
        {
            lock (_readLock)
            {
                if (_handleToInfoMap.ContainsKey(handle))
                {
                    Console.WriteLine($"[AFSHook] Read Request, Buffer: {(long)buffer:X}, Length: {length}, Offset (Cached from SetInformationFile): {_handleToInfoMap[handle].FilePointer}");
                }

                return _readFileHook.OriginalFunction(handle, hEvent, ref apcRoutine, ref apcContext, ref ioStatus, buffer, length, byteOffset, key);
            }
        }

        private int NtCreateFileImpl(out IntPtr handle, FileAccess access, ref Native.Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.Native.IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength)
        {
            lock (_createLock)
            {
                string oldFileName = objectAttributes.ObjectName.ToString();
                if (TryGetFullPath(oldFileName, out var newFilePath))
                {
                    // TODO: Add check for .afs file header.
                    if (newFilePath.Contains(".afs", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);
                        _handleToInfoMap[handle] = new FileInfo(newFilePath, 0);
                        Console.WriteLine($"[AFSHook] AFS File Handle Opened: {handle}, File: {newFilePath}");

                        return result;
                    }
                }

                return _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);
            }
        }

        /// <summary>
        /// Tries to resolve a given file path from NtCreateFile to a full file path.
        /// </summary>
        private bool TryGetFullPath(string oldFilePath, out string newFilePath)
        {
            if (oldFilePath.StartsWith("\\??\\", StringComparison.InvariantCultureIgnoreCase))
                oldFilePath = oldFilePath.Replace("\\??\\", "");

            if (!String.IsNullOrEmpty(oldFilePath))
            {
                newFilePath = Path.GetFullPath(oldFilePath);
                return true;
            }

            newFilePath = oldFilePath;
            return false;
        }
    }
}
