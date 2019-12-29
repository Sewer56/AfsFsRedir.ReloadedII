using System;
using System.Collections.Generic;
using System.Text;

namespace Afs.Hook.Test.Structs
{
    public class FileInfo
    {
        /// <summary>
        /// Contains the absolute file path to the file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Current read pointer for the file.
        /// </summary>
        public long FilePointer { get; set; }

        public FileInfo(string filePath, long filePointer)
        {
            FilePath = filePath;
            FilePointer = filePointer;
        }
    }
}
