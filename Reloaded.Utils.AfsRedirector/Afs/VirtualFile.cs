using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AFSLib.AfsStructs;

namespace Reloaded.Utils.AfsRedirector.Afs
{
    public class VirtualFile
    {
        private static Dictionary<string, IntPtr> _handleCache = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns true if this file is an external file outside of an AFS archive (offset == 0).
        /// Else returns false.
        /// </summary>
        public bool IsExternalFile => Offset == 0;

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

        /// <summary>
        /// Gets the handle used to access the file.
        /// </summary>
        public IntPtr Handle { get; private set; }

        public VirtualFile(int offset, int length, string filePath)
        {
            Offset = offset;
            Length = length;
            FilePath = filePath;
            SetHandle();
        }

        public VirtualFile(AfsFileEntry entry, string filePath)
        {
            Offset = entry.Offset;
            Length = entry.Length;
            FilePath = filePath;
            SetHandle();
        }

        public VirtualFile(string filePath)
        {
            Offset = 0;
            Length = (int) new System.IO.FileInfo(filePath).Length;
            FilePath = filePath;
            SetHandle();
        }

        private void SetHandle()
        {
            if (_handleCache.ContainsKey(FilePath))
            {
                Handle = _handleCache[FilePath];
            }
            else
            {
                Handle = CreateFileW(FilePath, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
                _handleCache[FilePath] = Handle;
            }
        }

        /// <summary>
        /// Reads the file from the hard disk.
        /// </summary>
        public unsafe byte[] GetData()
        {
            byte[] buffer = new byte[Length];
            fixed (byte* buf = buffer)
            {
                SetFilePointerEx(Handle, Offset, IntPtr.Zero, 0);
                ReadFile(Handle, buf, (uint) Length, out var bytesRead, IntPtr.Zero);
            }
            
            return buffer;
        }

        /// <summary>
        /// Reads the data from the hard disk, returning the number of read bytes into the buffer.
        /// </summary>
        public unsafe int GetData(Span<byte> buffer)
        {
            buffer = buffer.Slice(0, Length);
            fixed (byte* buf = buffer)
            {
                SetFilePointerEx(Handle, Offset, IntPtr.Zero, 0);
                ReadFile(Handle, buf, (uint)Length, out var bytesRead, IntPtr.Zero);
            }

            return Length;
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

        #region Native Imports
        [DllImport("kernel32.dll")]
        private static extern int SetFilePointerEx(IntPtr hFile, long liDistanceToMove, IntPtr lpNewFilePointer, uint dwMoveMethod);

        [DllImport("kernel32.dll")]
        private static extern unsafe bool ReadFile(IntPtr hFile, byte* lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string filename, FileAccess access, FileShare share, IntPtr securityAttributes, FileMode creationDisposition, FileAttributes flagsAndAttributes, IntPtr templateFile);
        #endregion
    }
}
