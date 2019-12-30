using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Afs.Hook.Test.Structs;
using AFSLib;
using AFSLib.AfsStructs;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using FileInfo = Afs.Hook.Test.Structs.FileInfo;

namespace Afs.Hook.Test
{
    /// <summary>
    /// Monitors calls to NtCreateFile, keeping track of all 
    /// </summary>
    public unsafe class AfsFileTracker
    {
        /// <summary>
        /// Executed when a handle to an AFS file is opened.
        /// </summary>
        public event AfsHandleOpened OnAfsHandleOpened = (path, handle) => { };

        /// <summary>
        /// Executed after data is read from an AFS file.
        /// </summary>
        public event AfsDataRead OnAfsDataRead = (handle, buffer, length, offset) => { };

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
                    long offset = _handleToInfoMap[handle].FilePointer;
                    Console.WriteLine($"[AFSHook] Read Request, Buffer: {(long)buffer:X}, Length: {length}, Offset (Cached from SetInformationFile): {offset}");
                    DisableRedirectionHooks();
                    OnAfsDataRead(handle, buffer, length, offset);
                    EnableRedirectionHooks();
                }

                return _readFileHook.OriginalFunction(handle, hEvent, ref apcRoutine, ref apcContext, ref ioStatus, buffer, length, byteOffset, key);
            }
        }

        private int NtCreateFileImpl(out IntPtr handle, FileAccess access, ref Native.Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.Native.IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength)
        {
            lock (_createLock)
            {
                string oldFileName = objectAttributes.ObjectName.ToString();
                if (!TryGetFullPath(oldFileName, out var newFilePath))
                    return _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);

                // Check if AFS file and register if it is.
                if (newFilePath.Contains(Constants.AfsExtension, StringComparison.OrdinalIgnoreCase))
                {
                    var result = _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);
                    DisableRedirectionHooks();
                    if (IsAfsFile(newFilePath))
                    {
                        Console.WriteLine($"[AFSHook] AFS File Handle Opened: {handle}, File: {newFilePath}");
                        _handleToInfoMap[handle] = new FileInfo(newFilePath, 0);
                        OnAfsHandleOpened(handle, newFilePath);
                    }
                    EnableRedirectionHooks();
                    return result;

                }

                return _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);
            }
        }

        private void DisableRedirectionHooks()
        {
            _createFileHook.Disable();
            _readFileHook.Disable();
        }

        private void EnableRedirectionHooks()
        {
            _readFileHook.Enable();
            _createFileHook.Enable();
        }

        /// <summary>
        /// Checks if a file at a specified path is an AFS archive.
        /// </summary>
        private bool IsAfsFile(string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, sizeof(AfsHeader));
            
            var data = new byte[sizeof(AfsHeader)];
            stream.Read(data, 0, data.Length);
            Struct.FromArray(data, out AfsHeader header);
            return header.IsAfsArchive;
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

        public delegate void AfsHandleOpened(IntPtr handle, string filePath);
        public delegate void AfsDataRead(IntPtr handle, byte* buffer, uint length, long offset);
    }
}
