using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibGit2Sharp.Core.Compat;

namespace LibGit2Sharp.Core
{
    class Pack
    {
        public string PackIdxFilePath { get; private set; }
        private readonly Lazy<ObjectDatabase> odb;
        private readonly Repository repo;

        internal Pack(Repository repo, string packIdxFilePath)
        {
            this.repo = repo;
            PackIdxFilePath = packIdxFilePath;
            odb = new Lazy<ObjectDatabase>(() => new ObjectDatabase(repo, this));
        }
    }
}
