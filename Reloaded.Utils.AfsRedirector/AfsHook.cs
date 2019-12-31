using System;
using System.Collections.Generic;
using System.IO;
using Reloaded.Utils.AfsRedirector.Afs;
using Reloaded.Utils.AfsRedirector.Structs;

namespace Reloaded.Utils.AfsRedirector
{
    /// <summary>
    /// FileSystem hook that redirects accesses to AFS file.
    /// </summary>
    public unsafe class AfsHook
    {
        private AfsFileTracker _afsFileTracker;

        private AfsBuilderCollection _builderCollection = new AfsBuilderCollection();
        private Dictionary<string, VirtualAfs> _virtualAfsFiles = new Dictionary<string, VirtualAfs>(StringComparer.OrdinalIgnoreCase);

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
            if (_afsFileTracker.TryGetInfoForHandle(handle, out var info))
            {
                if (!_virtualAfsFiles.ContainsKey(info.FilePath))
                {
                    numReadBytes = 0;
                    return false;
                }

                var afsFile = _virtualAfsFiles[info.FilePath];
                bool isHeaderRead = offset >= 0 && offset < afsFile.Header.Length;
                var bufferSpan = new Span<byte>(buffer, (int)length);

                if (isHeaderRead)
                {
                    // We are reading the file header, let's give the program the false header.
                    var fakeHeaderSpan = new Span<byte>(afsFile.HeaderPtr, afsFile.Header.Length);
                    var endOfHeader = offset + length;
                    if (endOfHeader > fakeHeaderSpan.Length)
                        length -= (uint)(endOfHeader - fakeHeaderSpan.Length);

                    var slice = fakeHeaderSpan.Slice((int)offset, (int)length);
                    slice.CopyTo(bufferSpan);

                    numReadBytes = slice.Length;
                    return true;
                }

                // We are reading a file, let's pass a new file to the buffer.
                if (afsFile.TryFindFile((int)offset, (int)length, out var virtualFile))
                {
                    numReadBytes = virtualFile.GetData(bufferSpan);
                    return true;
                }
            }

            numReadBytes = 0;
            return false;
        }

        /// <summary>
        /// When an AFS file is found, associate it with an existing virtual file.
        /// </summary>
        private void OnAfsHandleOpened(IntPtr handle, string filepath)
        {
            if (_virtualAfsFiles.ContainsKey(filepath))
                return;

#if DEBUG
            Console.WriteLine("------------ BUILDING AFS ------------");
#endif
            string fileName = Path.GetFileName(filepath);
            if (_builderCollection.TryGetBuilder(fileName, out var builder))
                _virtualAfsFiles[filepath] = builder.Build(filepath);
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
