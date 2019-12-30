using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Reloaded.Memory.Sources;

namespace Afs.Hook.Test.Native
{
    public unsafe class Native
    {
        /// <summary>
        /// Creates a new file or directory, or opens an existing file, device, directory, or volume.
        /// (The description here is a partial, lazy copy from MSDN)
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate int NtCreateFile(out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes, 
            ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions,
            IntPtr eaBuffer, uint eaLength);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate int NtReadFile(IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, 
            ref IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, long* byteOffset, IntPtr key);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate int SetFilePointer(IntPtr hFile, int liDistanceToMove, IntPtr lpNewFilePointer, uint dwMoveMethod);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate int NtSetInformationFile(IntPtr hFile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, FileInformationClass fileInformationClass);

        /// <summary>
        /// A driver sets an IRP's I/O status block to indicate the final status of an I/O request, before calling IoCompleteRequest for the IRP.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IO_STATUS_BLOCK
        {
            public UInt32 Status;
            public IntPtr Information;
        }

        /// <summary>
        /// The OBJECT_ATTRIBUTES structure specifies attributes that can be applied to objects or object
        /// handles by routines that create objects and/or return handles to objects.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_ATTRIBUTES : IDisposable
        {
            /// <summary>
            /// Lengthm of this structure.
            /// </summary>
            public int Length;

            /// <summary>
            /// Optional handle to the root object directory for the path name specified by the ObjectName member.
            /// If RootDirectory is NULL, ObjectName must point to a fully qualified object name that includes the full path to the target object.
            /// If RootDirectory is non-NULL, ObjectName specifies an object name relative to the RootDirectory directory.
            /// The RootDirectory handle can refer to a file system directory or an object directory in the object manager namespace.
            /// </summary>
            public IntPtr RootDirectory;

            /// <summary>
            /// Pointer to a Unicode string that contains the name of the object for which a handle is to be opened.
            /// This must either be a fully qualified object name, or a relative path name to the directory specified by the RootDirectory member.
            /// </summary>
            private IntPtr objectName;

            /// <summary>
            /// Bitmask of flags that specify object handle attributes. This member can contain one or more of the flags in the following table (See MSDN)
            /// </summary>
            public uint Attributes;

            /// <summary>
            /// Specifies a security descriptor (SECURITY_DESCRIPTOR) for the object when the object is created.
            /// If this member is NULL, the object will receive default security settings.
            /// </summary>
            public IntPtr SecurityDescriptor;

            /// <summary>
            /// Optional quality of service to be applied to the object when it is created.
            /// Used to indicate the security impersonation level and context tracking mode (dynamic or static).
            /// Currently, the InitializeObjectAttributes macro sets this member to NULL.
            /// </summary>
            public IntPtr SecurityQualityOfService;

            /// <summary>
            /// Gets or sets the file path of the files loaded in or out.
            /// </summary>
            public unsafe UNICODE_STRING ObjectName
            {
                get => *(UNICODE_STRING*)objectName;

                set
                {
                    // Check if we need to delete old memory.
                    bool fDeleteOld = objectName != IntPtr.Zero;

                    // Allocates the necessary bytes for the string.
                    if (!fDeleteOld)
                        objectName = Marshal.AllocHGlobal(Marshal.SizeOf(value));

                    // Deallocate old string while writing the new one.
                    Marshal.StructureToPtr(value, objectName, fDeleteOld);
                }
            }

            /// <summary>
            /// Disposes of the actual object name (file name) in question.
            /// </summary>
            public void Dispose()
            {
                if (objectName != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(objectName, typeof(UNICODE_STRING));
                    Marshal.FreeHGlobal(objectName);
                    objectName = IntPtr.Zero;
                }
            }
        }


        /// <summary>
        /// Does this really need to be explained to you?
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING : IDisposable
        {
            public ushort Length;
            public ushort MaximumLength;
            private IntPtr buffer;

            public UNICODE_STRING(string s)
            {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                buffer = Marshal.StringToHGlobalUni(s);
            }

            /// <summary>
            /// Disposes of the current file name assigned to this Unicode String.
            /// </summary>
            public void Dispose()
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            /// <summary>
            /// Returns a string with the contents
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                try
                {
                    if (buffer != IntPtr.Zero)
                    {
                        Memory.CurrentProcess.ReadRaw(buffer, out byte[] uniString, Length);
                        return Encoding.Unicode.GetString(uniString);
                    }

                    return "";
                }
                catch { return ""; }
            }
        }


        /// <summary>
        ///     Enumeration of the various file information classes.
        ///     See wdm.h.
        /// </summary>
        public enum FileInformationClass
        {
            None = 0,
            FileDirectoryInformation = 1,
            FileFullDirectoryInformation, // 2
            FileBothDirectoryInformation, // 3
            FileBasicInformation, // 4
            FileStandardInformation, // 5
            FileInternalInformation, // 6
            FileEaInformation, // 7
            FileAccessInformation, // 8
            FileNameInformation, // 9
            FileRenameInformation, // 10
            FileLinkInformation, // 11
            FileNamesInformation, // 12
            FileDispositionInformation, // 13
            FilePositionInformation, // 14
            FileFullEaInformation, // 15
            FileModeInformation, // 16
            FileAlignmentInformation, // 17
            FileAllInformation, // 18
            FileAllocationInformation, // 19
            FileEndOfFileInformation, // 20
            FileAlternateNameInformation, // 21
            FileStreamInformation, // 22
            FilePipeInformation, // 23
            FilePipeLocalInformation, // 24
            FilePipeRemoteInformation, // 25
            FileMailslotQueryInformation, // 26
            FileMailslotSetInformation, // 27
            FileCompressionInformation, // 28
            FileObjectIdInformation, // 29
            FileCompletionInformation, // 30
            FileMoveClusterInformation, // 31
            FileQuotaInformation, // 32
            FileReparsePointInformation, // 33
            FileNetworkOpenInformation, // 34
            FileAttributeTagInformation, // 35
            FileTrackingInformation, // 36
            FileIdBothDirectoryInformation, // 37
            FileIdFullDirectoryInformation, // 38
            FileValidDataLengthInformation, // 39
            FileShortNameInformation, // 40
            FileIoCompletionNotificationInformation, // 41
            FileIoStatusBlockRangeInformation, // 42
            FileIoPriorityHintInformation, // 43
            FileSfioReserveInformation, // 44
            FileSfioVolumeInformation, // 45
            FileHardLinkInformation, // 46
            FileProcessIdsUsingFileInformation, // 47
            FileNormalizedNameInformation, // 48
            FileNetworkPhysicalNameInformation, // 49
            FileIdGlobalTxDirectoryInformation, // 50
            FileIsRemoteDeviceInformation, // 51
            FileAttributeCacheInformation, // 52
            FileNumaNodeInformation, // 53
            FileStandardLinkInformation, // 54
            FileRemoteProtocolInformation, // 55
            FileMaximumInformation,
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
