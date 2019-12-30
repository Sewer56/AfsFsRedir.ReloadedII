using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AFSLib.AfsStructs;

namespace Afs.Hook.Test.Afs
{
    public class VirtualFile
    {
        /// <summary>
        /// Offset of the file inside the AFS archive.
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Length of the file inside the AFS archive.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Path to the file on the hard disk.
        /// </summary>
        public string FilePath { get; private set; }

        public VirtualFile(int offset, int length, string filePath)
        {
            Offset = offset;
            Length = length;
            FilePath = filePath;
        }

        public VirtualFile(AfsFileEntry entry, string filePath)
        {
            Offset = entry.Offset;
            Length = entry.Length;
            FilePath = filePath;
        }

        public VirtualFile(string filePath)
        {
            Offset = 0;
            Length = (int) new System.IO.FileInfo(filePath).Length;
            FilePath = filePath;
        }

        /// <summary>
        /// Reads the file from the hard disk.
        /// </summary>
        public byte[] GetData()
        {
            using FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192);
            stream.Seek(Offset, SeekOrigin.Begin);
            
            byte[] buffer = new byte[Length];
            stream.Read(buffer, 0, Length);
            return buffer;
        }

        /// <summary>
        /// Gets a <see cref="VirtualFile"/> that corresponds to a slice of this <see cref="VirtualFile"/>
        /// instance.
        /// </summary>
        /// <param name="offset">Offset of the slice relative to the current <see cref="Offset"/>.</param>
        /// <param name="length">Length of the slice starting from the current <see cref="Offset"/>.</param>
        /// <returns></returns>
        public VirtualFile Slice(int offset, int length)
        {
            // Error checking, just in case.
            var finalOffset = Offset + offset;
            if (finalOffset < Offset || finalOffset > Offset + Length)
                throw new ArgumentException("Requested offset if out of range. Is neither negative or beyond end of file.");

            var endOfFile = finalOffset + length;
            if (endOfFile < finalOffset || endOfFile > Offset + Length)
                throw new ArgumentException("Requested length if out of range. Is neither negative or will read beyond end of file.");

            return new VirtualFile(finalOffset, length, FilePath);
        }

        /// <summary>
        /// Gets a <see cref="VirtualFile"/> that corresponds to a slice of this <see cref="VirtualFile"/>.
        /// The length represents the maximum length of the slice. If the slice goes out of file range, the 
        /// length will be capped at the maximum possible value.
        /// </summary>
        /// <param name="offset">Offset of the slice relative to the current <see cref="Offset"/>.</param>
        /// <param name="length">Length of the slice starting from the current <see cref="Offset"/>.</param>
        /// <returns></returns>
        public VirtualFile SliceUpTo(int offset, int length)
        {
            // Error checking, just in case.
            var finalOffset = Offset + offset;
            if (finalOffset < Offset || finalOffset > Offset + Length)
                throw new ArgumentException("Requested offset if out of range. Is neither negative or beyond end of file.");

            var requestedEndOfFile = finalOffset + length;
            if (requestedEndOfFile < finalOffset)
                throw new ArgumentException("Requested length if out of range. It is negative.");

            var endOfFile = Offset + Length;
            if (requestedEndOfFile > endOfFile) 
                length -= requestedEndOfFile - endOfFile;

            return new VirtualFile(finalOffset, length, FilePath);
        }
    }
}
