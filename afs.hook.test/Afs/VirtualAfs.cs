using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Afs.Hook.Test.Afs
{
    public unsafe class VirtualAfs
    {
        /// <summary>
        /// A pointer to the virtual afs file header.
        /// </summary>
        public byte* VirtualAfsPtr { get; private set; }

        /// <summary>
        /// Contains a dictionary of custom or replaced AFS files.
        /// </summary>
        public Dictionary<int, VirtualFile> CustomFiles { get; private set; }

        /// <summary>
        /// Creates a Virtual AFS given the name of the file and the header of an AFS file.
        /// </summary>
        /// <param name="afsHeader">The bytes corresponding to the new AFS header.</param>
        /// <param name="customFiles">List of all custom files, mapped by index to file.</param>
        public VirtualAfs(byte[] afsHeader, Dictionary<int, VirtualFile> customFiles)
        {
            _afsHeader = afsHeader;
            CustomFiles = customFiles;

            _virtualAfsHandle = GCHandle.Alloc(_afsHeader, GCHandleType.Pinned);
            VirtualAfsPtr = (byte*) _virtualAfsHandle.Value.AddrOfPinnedObject();
        }

        private byte[] _afsHeader;
        private GCHandle? _virtualAfsHandle;
    }
}
