using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Afs.Hook.Test.Structs;
using AFSLib;
using AFSLib.AfsStructs;
using AFSLib.Helpers;
using Reloaded.Memory;

namespace Afs.Hook.Test.Afs
{
    /// <summary>
    /// Stores the information required to build a "Virtual AFS" file.
    /// </summary>
    public unsafe class VirtualAfsBuilder
    {
        private List<VirtualFile> _newFiles = new List<VirtualFile>();

        /// <summary>
        /// Adds a file to the Virtual AFS builder.
        /// </summary>
        public void AddOrReplaceFile(int index, string filePath)
        {
            if (index > ushort.MaxValue)
                throw new Exception($"Attempted to add file with index > {index}, this is not supported by the AFS container.");

            _newFiles.Add(new VirtualFile(index, filePath));
        }

        /// <summary>
        /// Builds a virtual AFS based upon a supplied base AFS file.
        /// </summary>
        public VirtualAfs Build(ICollection<AfsFileEntry> entries, int alignment = 2048)
        {
            // Get Custom File List
            var customFiles = new Dictionary<int, VirtualFile>(_newFiles.Count);

            foreach (var file in _newFiles) 
                customFiles[file.FileIndex] = file;

            // Get Original File List and Patch Where Necessary.
            var numFiles      = Math.Max(_newFiles.Max(x => x.FileIndex) + 1, entries.Count);
            var newEntries    = new AfsFileEntry[numFiles];
            entries.CopyTo(newEntries, entries.Count);

            foreach (var file in customFiles)
            {
                newEntries[file.Key] = new AfsFileEntry(0, (int) new System.IO.FileInfo(file.Value.FilePath).Length);
            }

            // Header
            using var memStream = new ExtendedMemoryStream(sizeof(AfsHeader) + (sizeof(AfsFileEntry) * _newFiles.Count) + alignment);
            memStream.Append(AfsHeader.FromNumberOfFiles(newEntries.Length));
            memStream.Append(newEntries);
            memStream.AddPadding(alignment);

            return new VirtualAfs(memStream.ToArray(), customFiles);
        }
    }
}
