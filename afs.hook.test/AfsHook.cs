using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Afs.Hook.Test.Afs;
using Afs.Hook.Test.Structs;
using AFSLib;
using AFSLib.AfsStructs;
using Microsoft.Win32.SafeHandles;
using Reloaded.Memory;

namespace Afs.Hook.Test
{
    /// <summary>
    /// FileSystem hook that redirects accesses to AFS file.
    /// </summary>
    public unsafe class AfsHook
    {
        private AfsFileTracker _afsFileTracker;

        private AfsBuilderCollection _builderCollection = new AfsBuilderCollection();
        private Dictionary<IntPtr, VirtualAfs> _virtualAfsFiles = new Dictionary<IntPtr, VirtualAfs>();

        public AfsHook(NativeFunctions functions)
        {
            _afsFileTracker = new AfsFileTracker(functions);
            _afsFileTracker.OnAfsHandleOpened += OnAfsHandleOpened;
            _afsFileTracker.OnAfsReadData += OnAfsReadData;
        }

        /// <summary>
        /// The evil one. Commits hard drive reading fraud!
        /// </summary>
        private bool OnAfsReadData(IntPtr handle, byte* buffer, uint length, long offset, out int numReadBytes)
        {
            if (!_virtualAfsFiles.ContainsKey(handle))
            {
                numReadBytes = 0;
                return false;
            }

            var afsFile       = _virtualAfsFiles[handle];
            bool isHeaderRead = offset >= 0 && offset < afsFile.Header.Length;
            var bufferSpan    = new Span<byte>(buffer, (int) length);

            if (isHeaderRead)
            {
                // We are reading the file header, let's give the program the false header.
                var fakeHeaderSpan = new Span<byte>(afsFile.HeaderPtr, afsFile.Header.Length);
                var endOfHeader = offset + length;
                if (endOfHeader > fakeHeaderSpan.Length)
                    length -= (uint)(endOfHeader - fakeHeaderSpan.Length);

                var slice = fakeHeaderSpan.Slice((int) offset, (int) length);
                slice.CopyTo(bufferSpan);

                numReadBytes = slice.Length;
                return true;
            }

            // We are reading a file, let's pass a new file to the buffer.
            if (afsFile.TryFindFile((int) offset, (int) length, out var virtualFile))
            {
                byte[] file = virtualFile.GetData();
                file.CopyTo(bufferSpan);

                numReadBytes = file.Length;
                return true;
            }

            numReadBytes = 0;
            return false;
        }

        /// <summary>
        /// When an AFS file is found, associate it with an existing virtual file.
        /// </summary>
        private void OnAfsHandleOpened(IntPtr handle, string filepath)
        {
            string fileName = Path.GetFileName(filepath);
            if (_builderCollection.TryGetBuilder(fileName, out var builder))
                _virtualAfsFiles[handle] = builder.Build(filepath, 2048);
        }

        /// <summary>
        /// Executed when a mod is loaded.
        /// </summary>
        /// <param name="modDirectory">The full path to the mod.</param>
        public void OnModLoading(string modDirectory)
        {
            if (Directory.Exists(GetRedirectPath(modDirectory)))
                _builderCollection.AddFromFolders(GetRedirectPath(modDirectory));
        }

        private string GetRedirectPath(string modFolder) => $"{modFolder}/{Constants.RedirectorFolderName}";
    }
}
