using System.Runtime.InteropServices;

namespace LibGit2Sharp.Core.Handles
{
    internal class PackBackendSafeHandle : SafeHandleBase
    {
        protected override bool ReleaseHandleImpl()
        {
            //TODO: Couldn't find native implementation
            return true;
        }
    }
}