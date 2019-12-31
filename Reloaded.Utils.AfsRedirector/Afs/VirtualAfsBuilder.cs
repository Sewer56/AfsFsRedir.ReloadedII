using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFSLib.AfsStructs;
using AFSLib.Helpers;
using Reloaded.Memory;
using Reloaded.Utils.AfsRedirector.Structs;

namespace Reloaded.Utils.AfsRedirector.Afs
{
    /// <summary>
    /// Stores the information required to build a "Virtual AFS" file.
    /// </summary>
    public unsafe class VirtualAfsBuilder
    {
        private Dictionary<int, Afs.VirtualFile> _customFiles = new Dictionary<int, VirtualFile>();

        /// <summary>
        /// Adds a file to the Virtual AFS builder.
        /// </summary>
        public void AddOrReplaceFile(int index, string filePath)
        {
            if (index > ushort.MaxValue)
                throw new Exception($"Attempted to add file with index > {index}, this is not supported by the AFS container.");

            _customFiles[index] = new VirtualFile(filePath);
        }

        /// <summary>
        /// Builds a virtual AFS based upon a supplied base AFS file.
        /// </summary>
        public VirtualAfs Build(string afsFilePath, int alignment = 2048)
        {
            // Get entries from original AFS file.
            var entries = GetEntriesFromFile(afsFilePath);
            var files   = new Dictionary<int, VirtualFile>(entries.Length);

            // Get Original File List and Copy to New Header.
            var maxCustomFileId = _customFiles.Count > 0 ? _customFiles.Max(x => x.Key) + 1 : 0;
            var numFiles      = Math.Max(maxCustomFileId, entries.Length);
            var newEntries    = new AfsFileEntry[numFiles];
            var headerLength  = Utilities.RoundUp(sizeof(AfsHeader) + (sizeof(AfsFileEntry) * entries.Length), alignment);

            // Create new Virtual AFS Header
            for (int x = 0; x < entries.Length; x++)
            {
                var offset = x > 0 ? Utilities.RoundUp(newEntries[x - 1].Offset + newEntries[x - 1].Length, alignment) : entries[0].Offset;
                int length = 0;

                if (_customFiles.ContainsKey(x))
                {
                    length = _customFiles[x].Length;
                    files[offset] = _customFiles[x];
                }
                else
                {
                    length = entries[x].Length;
                    files[offset] = new VirtualFile(entries[x], afsFilePath);
                }

                newEntries[x] = new AfsFileEntry(offset, length);
            }
            
            // Make Header
            using var memStream = new ExtendedMemoryStream(headerLength);
            memStream.Append(AfsHeader.FromNumberOfFiles(newEntries.Length));
            memStream.Append(newEntries);
            memStream.Append(new AfsFileEntry(0,0));
            memStream.AddPadding(alignment);

            return new VirtualAfs(memStream.ToArray(), files, alignment);
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
    }
}
