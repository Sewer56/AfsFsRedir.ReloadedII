using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFSLib.AfsStructs;
using AFSLib.Helpers;
using Reloaded.Memory;
using Reloaded.Utils.AfsRedirector.Structs;
using Reloaded.Utils.AfsRedirector.Utilities;

namespace Reloaded.Utils.AfsRedirector.Afs;

/// <summary>
/// Stores the information required to build a "Virtual AFS" file.
/// </summary>
public unsafe class VirtualAfsBuilder
{
    private readonly Dictionary<int, Afs.VirtualFile> _customFiles = new();

    /// <summary>
    /// Adds a file to the Virtual AFS builder.
    /// </summary>
    public void AddOrReplaceFile(int index, string filePath)
    {
        if (index > ushort.MaxValue)
            throw new($"Attempted to add file with index > {index}, this is not supported by the AFS container.");

        _customFiles[index] = new(filePath);
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
        var headerLength  = Structs.Utilities.RoundUp(sizeof(AfsHeader) + (sizeof(AfsFileEntry) * entries.Length), alignment);

        // Create new Virtual AFS Header
        for (int x = 0; x < entries.Length; x++)
        {
            var offset = x > 0 ? Structs.Utilities.RoundUp(newEntries[x - 1].Offset + newEntries[x - 1].Length, alignment) : entries[0].Offset;
            int length = 0;

            if (_customFiles.ContainsKey(x))
            {
                length = _customFiles[x].Length;
                files[offset] = _customFiles[x];
            }
            else
            {
                length = entries[x].Length;
                files[offset] = new(entries[x], afsFilePath);
            }

            newEntries[x] = new(offset, length);
        }

        var lastEntry = newEntries.Last(); 
        var fileSize  = Structs.Utilities.RoundUp(lastEntry.Offset + lastEntry.Length, alignment);

        // Make Header
        using var memStream = new ExtendedMemoryStream(headerLength);
        memStream.Append(AfsHeader.FromNumberOfFiles(newEntries.Length));
        memStream.Append(newEntries);
        memStream.Append(new AfsFileEntry(0,0));
        memStream.AddPadding(alignment);

        return new(memStream.ToArray(), files, alignment, fileSize);
    }

    /// <summary>
    /// Obtains the AFS header from a specific file path.
    /// </summary>
    private AfsFileEntry[] GetEntriesFromFile(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192);

        stream.TryReadSafe(out AfsHeader header);

        var data = new byte[sizeof(AfsFileEntry) * header.NumberOfFiles];
        stream.TryReadSafe(data);
        StructArray.FromArray(data, out AfsFileEntry[] entries);

        return entries;
    }
}