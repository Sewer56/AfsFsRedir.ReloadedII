using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Afs.Hook.Test.Structs;
using AFSLib;
using static Afs.Hook.Test.Structs.Utilities;

namespace Afs.Hook.Test.Afs
{
    public unsafe class VirtualAfs
    {
        /// <summary>
        /// Contains the data stored in the AFS header.
        /// </summary>
        public byte[] Header { get; private set; }

        /// <summary>
        /// A pointer to the virtual afs file header.
        /// </summary>
        public byte* HeaderPtr { get; private set; }

        /// <summary>
        /// Mapping of all files in the archive, offset to file.
        /// </summary>
        public Dictionary<int, VirtualFile> Files { get; private set; }

        /// <summary>
        /// The alignment of the files inside the archive.
        /// </summary>
        public int Alignment { get; private set; }

        private GCHandle? _virtualAfsHandle;

        /// <summary>
        /// Creates a Virtual AFS given the name of the file and the header of an AFS file.
        /// </summary>
        /// <param name="afsHeader">The bytes corresponding to the new AFS header.</param>
        /// <param name="files">Mapping of all files in the archive, offset to file.</param>
        /// <param name="alignment">Sets the alignment of the files inside the archive.</param>
        public VirtualAfs(byte[] afsHeader, Dictionary<int, VirtualFile> files, int alignment)
        {
            Header = afsHeader;
            Files = files;
            Alignment = alignment;

            _virtualAfsHandle = GCHandle.Alloc(Header, GCHandleType.Pinned);
            HeaderPtr = (byte*) _virtualAfsHandle.Value.AddrOfPinnedObject();
        }

        /// <summary>
        /// Gets a <see cref="VirtualFile"/> ready for reading given an offset and requested read length.
        /// </summary>
        /// <param name="offset">Offset of the file.</param>
        /// <param name="length">Length of the file.</param>
        /// <param name="file">The file, ready for reading.</param>
        public bool TryFindFile(int offset, int length, out VirtualFile file)
        {
            // O(1) if read is at known offset
            if (Files.ContainsKey(offset))
            {
                file = Files[offset];
                file = file.SliceUpTo(0, length);
                return true;
            }

            // Otherwise search one by one in O(N) fashion.
            if (AfsFileViewer.TryFromMemory(HeaderPtr, out var fileViewer))
            {
                var requestedReadRange = new AddressRange(offset, offset + length);
                foreach (var entry in fileViewer.Entries)
                {
                    var entryReadRange = new AddressRange(entry.Offset, RoundUp(entry.Offset + entry.Length, Alignment));
                    if (!entryReadRange.Contains(ref requestedReadRange)) 
                        continue;

                    int readOffset = requestedReadRange.Start - entryReadRange.Start;
                    int readLength = length;
                    file = Files[entryReadRange.Start];
                    file = file.SliceUpTo(readOffset, readLength);
                    return true;
                }
            }

            file = null;
            return false;
        }
    }
}
