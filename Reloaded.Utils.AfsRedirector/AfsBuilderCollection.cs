using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reloaded.Utils.AfsRedirector.Afs;

namespace Reloaded.Utils.AfsRedirector
{
    /// <summary>
    /// Encapsulates a group of AFS Builders, keyed by file names.
    /// </summary>
    public class AfsBuilderCollection
    {
        private Dictionary<string, VirtualAfsBuilder> _builders = new Dictionary<string, VirtualAfsBuilder>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns all of the builders held by this collection, mapped string to builder.
        /// </summary>
        /// <returns></returns>
        public KeyValuePair<string, VirtualAfsBuilder>[] GetAllBuilders() => _builders.ToArray();

        /// <summary>
        /// Adds files to the collection of builders given a directory containing directories
        /// corresponding to AFS archives.
        /// </summary>
        /// <param name="folder">Folder containing folders named after AFS archives.</param>
        public void AddFromFolders(string folder)
        {
            foreach (var directory in Directory.GetDirectories(folder))
            {
                if (!directory.EndsWith(Constants.AfsExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                var builder = GetBuilder(Path.GetFileName(directory));
                foreach (var file in Directory.GetFiles(directory))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (int.TryParse(fileName, out int index))
                        builder.AddOrReplaceFile(index, file);
                }
            }
        }

        /// <summary>
        /// Tries to get a <see cref="VirtualAfsBuilder"/> for the given file name.
        /// Returns true on success, false on failure.
        /// </summary>
        /// <param name="afsFileName">The file name of the AFS, including extension. Case insensitive.</param>
        public bool TryGetBuilder(string afsFileName, out VirtualAfsBuilder builder)
        {
            builder = _builders.ContainsKey(afsFileName) ? _builders[afsFileName] : null;
            return builder != null;
        }

        /// <summary>
        /// Gets a <see cref="VirtualAfsBuilder"/> for the given name or creates one if one does not exist.
        /// </summary>
        /// <param name="afsFileName">The file name of the AFS, including extension. Case insensitive.</param>
        public VirtualAfsBuilder GetBuilder(string afsFileName)
        {
            if (!_builders.ContainsKey(afsFileName)) 
                _builders[afsFileName] = new VirtualAfsBuilder();

            return _builders[afsFileName];
        }
    }
}
