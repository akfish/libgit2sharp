﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp.Core;
using Xunit;

namespace LibGit2Sharp.Tests.TestHelpers
{
    public class BaseFixture : IPostTestDirectoryRemover, IDisposable
    {
        private readonly List<string> directories = new List<string>();

        static BaseFixture()
        {
            // Do the set up in the static ctor so it only happens once
            SetUpTestEnvironment();

            DirectoryHelper.DeleteSubdirectories(Constants.TemporaryReposPath);
        }

        public static string BareTestRepoPath { get; private set; }
        public static string StandardTestRepoWorkingDirPath { get; private set; }
        public static string StandardTestRepoPath { get; private set; }
        public static string ShallowTestRepoPath { get; private set; }
        public static string MergedTestRepoWorkingDirPath { get; private set; }
        public static string SubmoduleTestRepoWorkingDirPath { get; private set; }
        public static DirectoryInfo ResourcesDirectory { get; private set; }

        public static bool IsFileSystemCaseSensitive { get; private set; }

        protected static DateTimeOffset TruncateSubSeconds(DateTimeOffset dto)
        {
            int seconds = dto.ToSecondsSinceEpoch();
            return Epoch.ToDateTimeOffset(seconds, (int) dto.Offset.TotalMinutes);
        }

        private static void SetUpTestEnvironment()
        {
            IsFileSystemCaseSensitive = IsFileSystemCaseSensitiveInternal();

            var source = new DirectoryInfo(@"../../Resources");
            ResourcesDirectory = new DirectoryInfo(string.Format(@"Resources/{0}", Guid.NewGuid()));
            var parent = new DirectoryInfo(@"Resources");

            if (parent.Exists)
            {
                DirectoryHelper.DeleteSubdirectories(parent.FullName);
            }

            DirectoryHelper.CopyFilesRecursively(source, ResourcesDirectory);

            // Setup standard paths to our test repositories
            BareTestRepoPath = Path.Combine(ResourcesDirectory.FullName, "testrepo.git");
            StandardTestRepoWorkingDirPath = Path.Combine(ResourcesDirectory.FullName, "testrepo_wd");
            StandardTestRepoPath = Path.Combine(StandardTestRepoWorkingDirPath, ".git");
            ShallowTestRepoPath = Path.Combine(ResourcesDirectory.FullName, "shallow.git");
            MergedTestRepoWorkingDirPath = Path.Combine(ResourcesDirectory.FullName, "mergedrepo_wd");
            SubmoduleTestRepoWorkingDirPath = Path.Combine(ResourcesDirectory.FullName, "submodule_wd");
        }

        private static bool IsFileSystemCaseSensitiveInternal()
        {
            var mixedPath = Path.Combine(Constants.TemporaryReposPath, "mIxEdCase");

            if (Directory.Exists(mixedPath))
            {
                Directory.Delete(mixedPath);
            }

            Directory.CreateDirectory(mixedPath);
            bool isInsensitive = Directory.Exists(mixedPath.ToLowerInvariant());

            Directory.Delete(mixedPath);

            return !isInsensitive;
        }

        protected void CreateCorruptedDeadBeefHead(string repoPath)
        {
            const string deadbeef = "deadbeef";
            string headPath = string.Format("refs/heads/{0}", deadbeef);

            Touch(repoPath, headPath, string.Format("{0}{0}{0}{0}{0}\n", deadbeef));
        }

        protected SelfCleaningDirectory BuildSelfCleaningDirectory()
        {
            return new SelfCleaningDirectory(this);
        }

        protected SelfCleaningDirectory BuildSelfCleaningDirectory(string path)
        {
            return new SelfCleaningDirectory(this, path);
        }

        protected string CloneBareTestRepo()
        {
            return Clone(BareTestRepoPath);
        }

        protected string CloneStandardTestRepo()
        {
            return Clone(StandardTestRepoWorkingDirPath);
        }

        protected string CloneMergedTestRepo()
        {
            return Clone(MergedTestRepoWorkingDirPath);
        }

        public string CloneSubmoduleTestRepo()
        {
            var submoduleTarget = Path.Combine(ResourcesDirectory.FullName, "submodule_target_wd");
            return Clone(SubmoduleTestRepoWorkingDirPath, submoduleTarget);
        }

        private string Clone(string sourceDirectoryPath, params string[] additionalSourcePaths)
        {
            var scd = BuildSelfCleaningDirectory();
            var source = new DirectoryInfo(sourceDirectoryPath);

            var clonePath = Path.Combine(scd.DirectoryPath, source.Name);
            DirectoryHelper.CopyFilesRecursively(source, new DirectoryInfo(clonePath));

            foreach (var additionalPath in additionalSourcePaths)
            {
                var additional = new DirectoryInfo(additionalPath);
                var targetForAdditional = Path.Combine(scd.DirectoryPath, additional.Name);

                DirectoryHelper.CopyFilesRecursively(additional, new DirectoryInfo(targetForAdditional));
            }

            return clonePath;
        }

        protected string InitNewRepository(bool isBare = false)
        {
            SelfCleaningDirectory scd = BuildSelfCleaningDirectory();

            return Repository.Init(scd.DirectoryPath, isBare);
        }

        public void Register(string directoryPath)
        {
            directories.Add(directoryPath);
        }

        public virtual void Dispose()
        {
#if LEAKS
            GC.Collect();
#endif

            foreach (string directory in directories)
            {
                DirectoryHelper.DeleteDirectory(directory);
            }
        }

        protected static void InconclusiveIf(Func<bool> predicate, string message)
        {
            if (!predicate())
            {
                return;
            }

            throw new SkipException(message);
        }

        protected void RequiresDotNetOrMonoGreaterThanOrEqualTo(Version minimumVersion)
        {
            Type type = Type.GetType("Mono.Runtime");

            if (type == null)
            {
                // We're running on top of .Net
                return;
            }

            MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);

            if (displayName == null)
            {
                throw new InvalidOperationException("Cannot access Mono.RunTime.GetDisplayName() method.");
            }

            var version = (string) displayName.Invoke(null, null);

            Version current;

            try
            {
                current = new Version(version.Split(' ')[0]);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Cannot parse Mono version '{0}'.", version), e);
            }

            InconclusiveIf(() => current < minimumVersion,
                string.Format(
                    "Current Mono version is {0}. Minimum required version to run this test is {1}.",
                    current, minimumVersion));
        }

        protected static void AssertValueInConfigFile(string configFilePath, string regex)
        {
            var text = File.ReadAllText(configFilePath);
            var r = new Regex(regex, RegexOptions.Multiline).Match(text);
            Assert.True(r.Success, text);
        }

        public RepositoryOptions BuildFakeConfigs(SelfCleaningDirectory scd)
        {
            var options = BuildFakeRepositoryOptions(scd);

            StringBuilder sb = new StringBuilder()
                .AppendFormat("[Woot]{0}", Environment.NewLine)
                .AppendFormat("this-rocks = global{0}", Environment.NewLine)
                .AppendFormat("[Wow]{0}", Environment.NewLine)
                .AppendFormat("Man-I-am-totally-global = 42{0}", Environment.NewLine);
            File.WriteAllText(options.GlobalConfigurationLocation, sb.ToString());

            sb = new StringBuilder()
                .AppendFormat("[Woot]{0}", Environment.NewLine)
                .AppendFormat("this-rocks = system{0}", Environment.NewLine);
            File.WriteAllText(options.SystemConfigurationLocation, sb.ToString());

            sb = new StringBuilder()
                .AppendFormat("[Woot]{0}", Environment.NewLine)
                .AppendFormat("this-rocks = xdg{0}", Environment.NewLine);
            File.WriteAllText(options.XdgConfigurationLocation, sb.ToString());

            return options;
        }

        private static RepositoryOptions BuildFakeRepositoryOptions(SelfCleaningDirectory scd)
        {
            string confs = Path.Combine(scd.DirectoryPath, "confs");
            Directory.CreateDirectory(confs);

            string globalLocation = Path.Combine(confs, "my-global-config");
            string xdgLocation = Path.Combine(confs, "my-xdg-config");
            string systemLocation = Path.Combine(confs, "my-system-config");

            return new RepositoryOptions
            {
                GlobalConfigurationLocation = globalLocation,
                XdgConfigurationLocation = xdgLocation,
                SystemConfigurationLocation = systemLocation,
            };
        }

        protected static string Touch(string parent, string file, string content = null, Encoding encoding = null)
        {
            string filePath = Path.Combine(parent, file);
            string dir = Path.GetDirectoryName(filePath);
            Debug.Assert(dir != null);

            Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, content ?? string.Empty, encoding ?? Encoding.ASCII);

            return filePath;
        }

        protected static string Touch(string parent, string file, Stream stream)
        {
            Debug.Assert(stream != null);

            string filePath = Path.Combine(parent, file);
            string dir = Path.GetDirectoryName(filePath);
            Debug.Assert(dir != null);

            Directory.CreateDirectory(dir);

            using (var fs = File.Open(filePath, FileMode.Create))
            {
                CopyStream(stream, fs);
                fs.Flush();
            }

            return filePath;
        }

        protected string Expected(string filename)
        {
            return File.ReadAllText(Path.Combine(ResourcesDirectory.FullName, "expected/" + filename));
        }

        protected string Expected(string filenameFormat, params object[] args)
        {
            return Expected(string.Format(CultureInfo.InvariantCulture, filenameFormat, args));
        }

        protected static void AssertRefLogEntry(Repository repo, string canonicalName,
                                                ObjectId to, string message, ObjectId @from = null,
                                                Signature committer = null)
        {
            var reflogEntry = repo.Refs.Log(canonicalName).First();

            Assert.Equal(to, reflogEntry.To);
            Assert.Equal(message, reflogEntry.Message);
            Assert.Equal(@from ?? ObjectId.Zero, reflogEntry.From);

            if (committer == null)
            {
                Assert.NotNull(reflogEntry.Commiter.Email);
                Assert.NotNull(reflogEntry.Commiter.Name);
            }
            else
            {
                Assert.Equal(committer, reflogEntry.Commiter);
            }
        }

        protected static void EnableRefLog(Repository repository, bool enable = true)
        {
            repository.Config.Set("core.logAllRefUpdates", enable);
        }

        public static void CopyStream(Stream input, Stream output)
        {
            // Reused from the following Stack Overflow post with permission
            // of Jon Skeet (obtained on 25 Feb 2013)
            // http://stackoverflow.com/questions/411592/how-do-i-save-a-stream-to-a-file/411605#411605
            var buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }

        public static bool StreamEquals(Stream one, Stream two)
        {
            int onebyte, twobyte;

            while ((onebyte = one.ReadByte()) >= 0 && (twobyte = two.ReadByte()) >= 0)
            {
                if (onebyte != twobyte)
                    return false;
            }

            return true;
        }
    }
}
