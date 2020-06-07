using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using AFSLib.AfsStructs;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using Reloaded.Utils.AfsRedirector.Structs;
using FileInfo = Reloaded.Utils.AfsRedirector.Structs.FileInfo;

namespace Reloaded.Utils.AfsRedirector
{
    public unsafe class AfsFileTracker
    {
        /// <summary>
        /// Executed when a handle to an AFS file is opened.
        /// </summary>
        public event AfsHandleOpened OnAfsHandleOpened = (path, handle) => { };

        /// <summary>
        /// Gets the size of a virtual AFS file.
        /// </summary>
        public event AfsGetSize OnGetAfsSize = (handle) => 0;

        /// <summary>
        /// Executed after data is read from an AFS file.
        /// </summary>
        public event AfsDataRead OnAfsReadData = (IntPtr handle, byte* buffer, uint length, long offset, out int numReadBytes) =>
        {
            numReadBytes = 0;
            return false;
        };

        /// <summary>
        /// Maps file handles to file paths.
        /// </summary>
        private ConcurrentDictionary<IntPtr, FileInfo> _handleToInfoMap = new ConcurrentDictionary<IntPtr, FileInfo>();
        private Dictionary<string, bool> _isAfsFileCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private IHook<Native.Native.NtCreateFile> _createFileHook;
        private IHook<Native.Native.NtReadFile> _readFileHook;
        private IHook<Native.Native.NtSetInformationFile> _setFilePointerHook;
        private IHook<Native.Native.NtQueryInformationFile> _getFileSizeHook;

        private object _createLock = new object();
        private object _getInfoLock = new object();
        private object _setInfoLock = new object();
        private object _readLock = new object();

        public AfsFileTracker(NativeFunctions functions)
        {
            _createFileHook = functions.NtCreateFile.Hook(NtCreateFileImpl).Activate();
            _readFileHook = functions.NtReadFile.Hook(NtReadFileImpl).Activate();
            _setFilePointerHook = functions.SetFilePointer.Hook(SetInformationFileImpl).Activate();
            _getFileSizeHook = functions.GetFileSize.Hook(QueryInformationFileHook).Activate();

            // TODO: Hook NtClose
            // Problem: Native->Managed Transition hits NtClose in .NET Core, so our hook code is never hit.
            // Problem: NtClose needs synchronization.
            // Solution: Write custom ASM to solve the problem, see NtClose branch.
        }

        /// <summary>
        /// Tries to get the information for a file behind a handle.
        /// </summary>
        public bool TryGetInfoForHandle(IntPtr handle, out FileInfo info)
        {
            info = null;

            if (!_handleToInfoMap.ContainsKey(handle))
                return false;

            info = _handleToInfoMap[handle];
            return true;
        }

        private int QueryInformationFileHook(IntPtr hfile, out Native.Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.Native.FileInformationClass fileInformationClass)
        {
            lock (_getInfoLock)
            {
                var result = _getFileSizeHook.OriginalFunction(hfile, out ioStatusBlock, fileInformation, length, fileInformationClass);

                if (_handleToInfoMap.ContainsKey(hfile) && fileInformationClass == Native.Native.FileInformationClass.FileStandardInformation)
                {
                    var information = (Native.Native.FILE_STANDARD_INFORMATION*)fileInformation;
                    var oldSize = information->EndOfFile;
                    var newSize = OnGetAfsSize(hfile);
                    if (newSize != -1)
                        information->EndOfFile = newSize;

                    #if DEBUG
                    Console.WriteLine($"File Size Override | Old: {oldSize}, New: {information->EndOfFile} | {_handleToInfoMap[hfile].FilePath}");
                    #endif
                }

                return result;
            }
        }

        private int SetInformationFileImpl(IntPtr hfile, out Native.Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.Native.FileInformationClass fileInformationClass)
        {
            lock (_setInfoLock)
            {
                if (_handleToInfoMap.ContainsKey(hfile) && fileInformationClass == Native.Native.FileInformationClass.FilePositionInformation)
                {
                    var pointer = *(long*)fileInformation;
                    _handleToInfoMap[hfile].FilePointer = pointer;
                }

                return _setFilePointerHook.OriginalFunction(hfile, out ioStatusBlock, fileInformation, length, fileInformationClass);
            }
        }

        private unsafe int NtReadFileImpl(IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, ref Native.Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, long* byteOffset, IntPtr key)
        {
            lock (_readLock)
            {
                if (_handleToInfoMap.ContainsKey(handle))
                {
                    long offset          = _handleToInfoMap[handle].FilePointer;
                    long requestedOffset = byteOffset != (void*) 0 ? *byteOffset : -1;
                    #if DEBUG
                    Console.WriteLine($"[AFSHook] Read Request, Buffer: {(long)buffer:X}, Length: {length}, Offset (via SetInformationFile): {offset}, Requested Offset (Optional): {requestedOffset}");
                    #endif
                    
                    DisableRedirectionHooks();
                    bool result;
                    int numReadBytes;
                    result = requestedOffset != -1 ? OnAfsReadData(handle, buffer, length, requestedOffset, out numReadBytes) 
                                                   : OnAfsReadData(handle, buffer, length, offset, out numReadBytes);
                    EnableRedirectionHooks();

                    if (result)
                    {
                        offset += length;
                        SetInformationFileImpl(handle, out _, &offset, sizeof(long), Native.Native.FileInformationClass.FilePositionInformation);

                        // Set number of read bytes.
                        ioStatus.Status      = 0;
                        ioStatus.Information = new IntPtr(numReadBytes);
                        return 0;
                    }
                }

                return _readFileHook.OriginalFunction(handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key);
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
                        #if DEBUG
                        Console.WriteLine($"[AFSHook] AFS File Handle Opened: {handle}, File: {newFilePath}");
                        #endif
                        _handleToInfoMap[handle] = new FileInfo(newFilePath, 0);
                        OnAfsHandleOpened(handle, newFilePath);
                    }
                    EnableRedirectionHooks();
                    return result;
                }

                var ntStatus = _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);

                // Invalidate Duplicate Handles (until we implement NtClose hook).
                if (_handleToInfoMap.ContainsKey(handle))
                {
                    _handleToInfoMap.TryRemove(handle, out var value);
                    #if DEBUG
                    Console.WriteLine($"[AFSHook] Removed old disposed handle: {handle}, File: {value.FilePath}");
                    #endif
                }

                return ntStatus;
            }
        }

        private void DisableRedirectionHooks()
        {
            _createFileHook.Disable();
            _readFileHook.Disable();
            _getFileSizeHook.Disable();
        }

        private void EnableRedirectionHooks()
        {
            _readFileHook.Enable();
            _createFileHook.Enable();
            _getFileSizeHook.Enable();
        }

        /// <summary>
        /// Checks if a file at a specified path is an AFS archive.
        /// </summary>
        private bool IsAfsFile(string filePath)
        {
            if (_isAfsFileCache.ContainsKey(filePath))
                return _isAfsFileCache[filePath];

            if (!File.Exists(filePath))
                return false;

            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, sizeof(AfsHeader));
            
            var data     = new byte[sizeof(AfsHeader)];
            var dataSpan = data.AsSpan();
            stream.Read(dataSpan);
            Struct.FromArray(dataSpan, out AfsHeader header);
            
            bool isAfsFile = header.IsAfsArchive;
            _isAfsFileCache[filePath] = isAfsFile;
            return isAfsFile;
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
        public delegate bool AfsDataRead(IntPtr handle, byte* buffer, uint length, long offset, out int numReadBytes);
        public delegate int  AfsGetSize(IntPtr handle);
    }
}
