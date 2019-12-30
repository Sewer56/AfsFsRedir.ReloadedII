using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Afs.Hook.Test.Afs;
using Afs.Hook.Test.Structs;
using AFSLib;
using AFSLib.AfsStructs;
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
        }

        /// <summary>
        /// When an AFS file is found, associate it with an existing virtual file.
        /// </summary>
        private void OnAfsHandleOpened(IntPtr handle, string filepath)
        {
            string fileName = Path.GetFileName(filepath);
            if (_builderCollection.TryGetBuilder(fileName, out var builder))
                _virtualAfsFiles[handle] = builder.Build(GetEntriesFromFile(filepath));
        }

        /// <summary>
        /// Obtains the AFS header from a specific file path.
        /// </summary>
        private AfsFileEntry[] GetEntriesFromFile(string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192);

            var data = new byte[sizeof(AfsHeader)];
            stream.Read(data, 0, data.Length);
            Struct.FromArray(data, out AfsHeader header);

            data = new byte[sizeof(AfsFileEntry) * header.NumberOfFiles];
            stream.Read(data, 0, data.Length);
            StructArray.FromArray(data, out AfsFileEntry[] entries);

            return entries;
        }

        /// <summary>
        /// Executed when a mod is loaded.
        /// </summary>
        /// <param name="modDirectory">The full path to the mod.</param>
        public void OnModLoading(string modDirectory)
        {
            if (!Directory.Exists(GetRedirectPath(modDirectory)))
                _builderCollection.AddFromFolders(GetRedirectPath(modDirectory));
        }

        private string GetRedirectPath(string modFolder) => $"{modFolder}/{Constants.RedirectorFolderName}";
    }
}
