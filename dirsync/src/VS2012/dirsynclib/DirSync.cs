﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dirsynclib
{
    public enum SyncPolicy
    {
        Full,
        Differential
    }

    public enum MessageLevel
    {
        Debug,
        Information,
        FileIO,
        WARNING,
        ERROR
    }

    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs() : base() { }

        public DirSync Source { get; set; }
        public MessageLevel Level { get; set; }
        public string Text { get; set; }
    }

    public delegate void MessageEventHandler(object sender, MessageEventArgs e);

    public class DirSync
    {
        #region Data Members

        const string kSystemPathPattern = @"^[A-Z]\:(\\|\/)Windows(\\|\/)";

        //private bool _caseSensitive;
        SyncPolicy _syncPolicy;
        MessageLevel _verbosity;

        #endregion

        #region Constructors

        public DirSync()
        {
            _syncPolicy = SyncPolicy.Differential;
            _verbosity = MessageLevel.FileIO;
        }

        #endregion

        #region Events

        public event MessageEventHandler Message;

        #endregion

        #region Properties

        ///// <summary>
        ///// Specifies that the sync should treat include/exclude patterns and file names with case-sensitivity.
        ///// </summary>
        //public bool CaseSensitive 
        //{
        //    get { return _caseSensitive; }
        //    set { _caseSensitive = value; }
        //}

        /// <summary>
        /// Gets or sets the sync policy (full vs. differential) when Sync(...) is called.
        /// </summary>
        public SyncPolicy SyncPolicy
        {
            get { return _syncPolicy; }
            set { _syncPolicy = value; }
        }

        /// <summary>
        /// Gets or sets the minimum message level that will be printed into the buffer when Sync(...) is called.
        /// </summary>
        public MessageLevel Verbosity
        {
            get { return _verbosity; }
            set { _verbosity = value; }
        }

        #endregion

        #region Non-public Methods

        private void BroadcastMessage(MessageLevel level, string msg, params object[] args)
        {
            if ((int)level >= (int)_verbosity)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("[{0:hh:mm:ss tt}] {1}: {2}", 
                    DateTime.Now, level.ToString(), string.Format(msg, args)));

                if (Message != null)
                {
                    Message(this, new MessageEventArgs() { Level = level, Source = this, Text = string.Format(msg, args) });
                }
            }
        }

        private void SyncDirectory(string sourcePath, string destPath, IEnumerable<string> excludePatterns)
        {
            foreach (string patt in excludePatterns)
            {
                if (Regex.IsMatch(sourcePath, patt, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return;
                }
            }

            try
            {
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                    BroadcastMessage(MessageLevel.FileIO, "Created directory '{0}'.", destPath);
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while creating directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var dir in new DirectoryInfo(sourcePath).GetDirectories())
                {
                    try
                    {
                        SyncDirectory(dir.FullName, destPath + "\\" + dir.Name, excludePatterns);
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, dir.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing directories in '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var dir in new DirectoryInfo(destPath).GetDirectories())
                {
                    try
                    {
                        if (!Directory.Exists(sourcePath + "\\" + dir.Name))
                        {
                            DeleteDirectory(destPath + "\\" + dir.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + dir.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var file in new DirectoryInfo(sourcePath).GetFiles())
                {
                    try
                    {
                        SyncFile(sourcePath + "\\" + file.Name, destPath + "\\" + file.Name, excludePatterns);
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + file.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var file in new DirectoryInfo(destPath).GetFiles())
                {
                    try
                    {
                        if (Regex.IsMatch(sourcePath + "\\" + file.Name, kSystemPathPattern))
                        {
                            throw new Exception(string.Format("File '{0}' appears to be contained within a system directory.", sourcePath + "\\" + file.Name));
                        }

                        if (!File.Exists(sourcePath + "\\" + file.Name))
                        {
                            File.Delete(destPath + "\\" + file.Name);
                            BroadcastMessage(MessageLevel.FileIO, "Deleted file '{0}'.", destPath + "\\" + file.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + file.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            BroadcastMessage(MessageLevel.FileIO, "Synced directory '{0}' => '{1}'.", sourcePath, destPath);
        }

        private void SyncFile(string sourcePath, string destPath, IEnumerable<string> excludePatterns)
        {
            foreach (string patt in excludePatterns)
            {
                if (Regex.IsMatch(sourcePath, patt, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    BroadcastMessage(MessageLevel.Information, "'{0}' matched pattern '{1}' -- skipping file.", sourcePath, patt);
                    return;
                }
            }

            try
            {
                if (File.Exists(destPath))
                {
                    if (_syncPolicy == SyncPolicy.Differential)
                    {
                        var hashA = "";
                        var hashB = "";

                        try
                        {
                            using (FileStream streamA = File.OpenRead(sourcePath))
                            using (FileStream streamB = File.OpenRead(destPath))
                            {
                                SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider();

                                byte[] byteHashA = sha.ComputeHash(streamA);
                                hashA = BitConverter.ToString(byteHashA).Replace("-", String.Empty);

                                byte[] byteHashB = sha.ComputeHash(streamB);
                                hashB = BitConverter.ToString(byteHashB).Replace("-", String.Empty);
                            }

                            if (hashA == hashB)
                            {
                                BroadcastMessage(MessageLevel.Information, "'{0}' is binary equal to '{1}' -- skipping file.", sourcePath, destPath);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            BroadcastMessage(MessageLevel.ERROR, "{0} caught while comparing files '{1}' and '{2}': {3}", ex.GetType().Name, sourcePath, destPath, ex.Message);
                            return;
                        }
                    }
                }

                File.Copy(sourcePath, destPath, true);
                BroadcastMessage(MessageLevel.FileIO, "Synced file '{0}' => '{1}'.", sourcePath, destPath);
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }
        }

        private void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            if (Regex.IsMatch(path, kSystemPathPattern))
            {
                throw new Exception(string.Format("Path '{0}' appears to be a system directory.", path));
            }

            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        DeleteDirectory(dir);
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, dir, ex.Message);
                    }
                }

                foreach (string file in Directory.GetFiles(path))
                {
                    try 
                    {
                        File.Delete(file);
                        BroadcastMessage(MessageLevel.FileIO, "Deleted file '{0}'", file);
                    }
                    catch (Exception ex)
                    {
                        BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting file '{1}': {2}", ex.GetType().Name, file, ex.Message);
                    }
                }

                try
                {
                    Directory.Delete(path);
                    BroadcastMessage(MessageLevel.FileIO, "Deleted directory '{0}'", path);
                }
                catch (Exception ex)
                {
                    BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, path, ex.Message);
                }
            }
            catch (Exception ex)
            {
                BroadcastMessage(MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, path, ex.Message);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Syncs the destination with the source.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        public void Sync(string sourcePath, string destPath, IList<string> excludePatterns)
        {
            for (int i = 0; i < excludePatterns.Count(); i++)
            {
                excludePatterns[i] = excludePatterns[i].Replace("*", ".*").Replace(@"\", @"\\");
            }

            if (Directory.Exists(sourcePath))
            {
                try
                {
                    SyncDirectory(sourcePath, destPath, excludePatterns);
                }
                catch (Exception ex)
                {
                    BroadcastMessage(MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
                }
            }
            else if (File.Exists(sourcePath))
            {
                try
                {
                    SyncFile(sourcePath, destPath, excludePatterns);
                }
                catch (Exception ex)
                {
                    BroadcastMessage(MessageLevel.ERROR, "{0} caught while file directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
                }
            }
            else
            {
                throw new FileNotFoundException(sourcePath);
            }
        }

        #endregion
    }
}
