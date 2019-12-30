using System;
using System.Collections.Generic;
using System.Text;

namespace Afs.Hook.Test.Afs
{
    public class VirtualFile
    {
        /// <summary>
        /// Index of the file inside the AFS archive.
        /// </summary>
        public int FileIndex { get; set; }

        /// <summary>
        /// Path to the file on the hard disk.
        /// </summary>
        public string FilePath { get; set; }

        public VirtualFile(int fileIndex, string filePath)
        {
            FileIndex = fileIndex;
            FilePath = filePath;
        }
    }
}
