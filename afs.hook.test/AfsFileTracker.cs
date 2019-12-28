using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Afs.Hook.Test.Pointers;
using Reloaded.Hooks.Definitions;

namespace Afs.Hook.Test
{
    /// <summary>
    /// Monitors calls to NtCreateFile, keeping track of all 
    /// </summary>
    public class AfsFileTracker
    {
        /// <summary>
        /// Maps file handles to file paths.
        /// </summary>
        private ConcurrentDictionary<IntPtr, string> _handleToPathMap = new ConcurrentDictionary<IntPtr, string>();
        private IHook<Native.Native.NtCreateFile> _createFileHook;
        private object _lock = new object();

        public AfsFileTracker(NativeFunctions functions, IReloadedHooks hooks)
        {
            _createFileHook = functions.NtCreateFile.Hook(NtCreateFileImpl).Activate();
        }

        private int NtCreateFileImpl(out IntPtr handle, FileAccess access, ref Native.Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.Native.IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength)
        {
            lock (_lock)
            {
                string oldFileName = objectAttributes.ObjectName.ToString();
                if (TryGetFullPath(oldFileName, out var newFilePath))
                {
                    // TODO: Add check for .afs file header.
                    if (newFilePath.Contains(".afs", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = _createFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);
                        _handleToPathMap[handle] = newFilePath;
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
