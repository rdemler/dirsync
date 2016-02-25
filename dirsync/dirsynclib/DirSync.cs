using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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

    public class DirSync
    {

        #region Data Members

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

        private void AddBufferMsg(ref IList<string> buffer, MessageLevel level, string msg, params object[] args)
        {
            if ((int)level >= (int)_verbosity)
            {
                buffer.Add(string.Format("[{0:hh:mm:ss tt}] {1}: {2}", DateTime.Now, level.ToString(), string.Format(msg, args)));
            }
        }

        private void SyncDirectory(string sourcePath, string destPath, ref IList<string> buffer)
        {
            try
            {
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                    AddBufferMsg(ref buffer, MessageLevel.FileIO, "Created directory '{0}'.", destPath);
                }
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while creating directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var dir in new DirectoryInfo(sourcePath).GetDirectories())
                {
                    try
                    {
                        SyncDirectory(dir.FullName, destPath + "\\" + dir.Name, ref buffer);
                    }
                    catch (Exception ex)
                    {
                        AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, dir.FullName, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing directories in '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var dir in new DirectoryInfo(destPath).GetDirectories())
                {
                    try
                    {
                        if (!Directory.Exists(sourcePath + "\\" + dir.Name))
                        {
                            Directory.Delete(destPath + "\\" + dir.Name);
                            AddBufferMsg(ref buffer, MessageLevel.FileIO, "Deleted directory '{0}'.", destPath + "\\" + dir.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + dir.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while deleting directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var file in new DirectoryInfo(sourcePath).GetFiles())
                {
                    try
                    {
                        SyncFile(sourcePath + "\\" + file.Name, destPath + "\\" + file.Name, ref buffer);
                    }
                    catch (Exception ex)
                    {
                        AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + file.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            try
            {
                foreach (var file in new DirectoryInfo(destPath).GetFiles())
                {
                    try
                    {
                        if (!File.Exists(sourcePath + "\\" + file.Name))
                        {
                            File.Delete(destPath + "\\" + file.Name);
                            AddBufferMsg(ref buffer, MessageLevel.FileIO, "Deleted file '{0}'.", destPath + "\\" + file.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath + "\\" + file.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }

            AddBufferMsg(ref buffer, MessageLevel.FileIO, "Synced directory '{0}' => '{1}'.", sourcePath, destPath);
        }

        private void SyncFile(string sourcePath, string destPath, ref IList<string> buffer)
        {
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
                                AddBufferMsg(ref buffer, MessageLevel.Information, "'{0}' is binary equal to '{1}' -- skipping file.", sourcePath, destPath);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while comparing files '{1}' and '{2}': {3}", ex.GetType().Name, sourcePath, destPath, ex.Message);
                            return;
                        }
                    }
                }

                File.Copy(sourcePath, destPath, true);
                AddBufferMsg(ref buffer, MessageLevel.FileIO, "Synced file '{0}' => '{1}'.", sourcePath, destPath);
            }
            catch (Exception ex)
            {
                AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing file '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Syncs the destination with the source.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        public IEnumerable<string> Sync(string sourcePath, string destPath)
        {
            IList<string> buffer = new List<string>();

            if (Directory.Exists(sourcePath))
            {
                try
                {
                    SyncDirectory(sourcePath, destPath, ref buffer);
                }
                catch (Exception ex)
                {
                    AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while syncing directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
                }
            }
            else if (File.Exists(sourcePath))
            {
                try
                {
                    SyncFile(sourcePath, destPath, ref buffer);
                }
                catch (Exception ex)
                {
                    AddBufferMsg(ref buffer, MessageLevel.ERROR, "{0} caught while file directory '{1}': {2}", ex.GetType().Name, sourcePath, ex.Message);
                }
            }
            else
            {
                throw new FileNotFoundException(sourcePath);
            }

            return buffer;
        }

        #endregion
    }
}
