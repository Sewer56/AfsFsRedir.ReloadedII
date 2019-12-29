using System;
using System.Collections.Generic;
using System.Text;
using Afs.Hook.Test.Structs;

namespace Afs.Hook.Test
{
    /// <summary>
    /// FileSystem hook that redirects accesses to AFS file.
    /// </summary>
    public class AfsHook
    {
        private AfsFileTracker _afsFileTracker;

        public AfsHook(NativeFunctions functions)
        {
            _afsFileTracker = new AfsFileTracker(functions);
        }
    }
}
