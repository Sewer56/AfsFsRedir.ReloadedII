using System.Collections.Generic;
using System.Runtime.InteropServices;
using AFSLib;
using Reloaded.Utils.AfsRedirector.Structs;
using static Reloaded.Utils.AfsRedirector.Structs.Utilities;

namespace Reloaded.Utils.AfsRedirector.Afs
{
    public unsafe class VirtualAfs
    {
        private const int LastOffetsNum = 4;

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
        private int[] _recentOffsets = new int[LastOffetsNum];
        private int _lastOffsetIndex = 0;

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
            // O(1) if read is at known offset. Most likely to occur.
            if (Files.ContainsKey(offset))
            {
                // Cache for reading.
                _recentOffsets[_lastOffsetIndex++] = offset;
                _lastOffsetIndex %= _recentOffsets.Length;

                // Return offsets :P
                file = Files[offset];
                file = file.SliceUpTo(0, length);
                return true;
            }

            // Try O(LastOffetsNum) in very likely probability chunk recent file requested.
            var requestedReadRange = new AddressRange(offset, offset + length);
            foreach (var recentOffset in _recentOffsets)
            {
                var lastFile = Files[recentOffset];
                var lastFileOffset = lastFile.IsExternalFile ? recentOffset : lastFile.Offset;

                var entryReadRange = new AddressRange(lastFileOffset, RoundUp(lastFileOffset + lastFile.Length, Alignment));
                if (entryReadRange.Contains(ref requestedReadRange))
                    return ReturnFoundFile(entryReadRange, out file);
            }

            // Otherwise search one by one in O(N) fashion.
            if (AfsFileViewer.TryFromMemory(HeaderPtr, out var fileViewer))
            {
                foreach (var entry in fileViewer.Entries)
                {
                    var entryReadRange = new AddressRange(entry.Offset, RoundUp(entry.Offset + entry.Length, Alignment));
                    if (entryReadRange.Contains(ref requestedReadRange))
                        return ReturnFoundFile(entryReadRange, out file);
                }
            }

            // Returns a file if it has been found.
            bool ReturnFoundFile(AddressRange entryReadRange, out VirtualFile returnFile)
            {
                int readOffset = requestedReadRange.Start - entryReadRange.Start;
                int readLength = length;

                returnFile = Files[entryReadRange.Start];
                returnFile = returnFile.SliceUpTo(readOffset, readLength);
                return true;
            }

            file = null;
            return false;
        }
    }
}
