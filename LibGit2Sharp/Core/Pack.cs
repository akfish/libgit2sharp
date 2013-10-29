using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp.Core.Compat;

namespace LibGit2Sharp.Core
{
    public class Pack
    {
        public string PackIdxFilePath { get; private set; }
        private readonly Lazy<ObjectDatabase> odb;
        private readonly Repository repo;
        /// <summary>
        /// Gets the name of pack
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the database
        /// </summary>
        public ObjectDatabase ObjectDatabase
        {
            get
            {
                return odb.Value;
            }
        }

        internal Pack(Repository repo, string packIdxFilePath)
        {
            this.repo = repo;
            FileInfo fileInfo = new FileInfo(packIdxFilePath);
            PackIdxFilePath = packIdxFilePath;
            Name = fileInfo.Name;
            odb = new Lazy<ObjectDatabase>(() => new ObjectDatabase(repo, this));
        }
    }

    public class PackCollection : List<Pack>
    {
        internal const string PackFolder = @"objects\pack";
        private readonly Repository repo;
        private string packFolderPath;

        public PackCollection(Repository repo)
        {
            this.repo = repo;
            packFolderPath = repo.RepoPath + PackFolder;
            DirectoryInfo folder = new DirectoryInfo(packFolderPath);
            if (folder.Exists)
            {
                foreach (FileInfo fileInfo in folder.GetFiles("*.idx"))
                {
                    Add(new Pack(repo, fileInfo.FullName));
                }
            }
        }
    }
}
