using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PipelineCommon.Helpers
{
    /// <summary>
    /// A virtual file system that runs out of a zip file without extracting it
    /// </summary>
    public class ZipFileSystem
    {
        private const char Seperater = '/';
        ZipArchive _zip;
        Stream _stream;
        /// <summary>
        /// Create a new ZipFileSystem from a path
        /// </summary>
        /// <param name="path">The path of the zip file to load</param>
        public ZipFileSystem(string path)
        {
            _stream = File.OpenRead(path);
            _zip = new ZipArchive(_stream);
        }
        /// <summary>
        /// Create a new ZipFileSystem from a Stream
        /// </summary>
        /// <param name="stream">The stream to use for loading from</param>
        public ZipFileSystem(Stream stream)
        {
            _stream = stream;
            _zip = new ZipArchive(_stream);
        }

        /// <summary>
        /// Read all the text from a file in the zip
        /// </summary>
        /// <param name="file">An absolute path to the file in the zip</param>
        /// <returns>The contents of the file</returns>
        public string ReadAllText(string file)
        {
            using (StreamReader reader = new StreamReader(_zip.GetEntry(file).Open()))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Get all the files in the zip that optionally match a ending pattern
        /// </summary>
        /// <param name="pattern">An optional ending pattern to match</param>
        /// <returns>A Enumberable of absolute paths to files</returns>
        public IEnumerable<string> GetAllFiles(string pattern = null)
        {
            if (pattern == null)
            {
                return _zip.Entries.Select(e => e.FullName);
            }

            return _zip.Entries.Select(e => e.FullName).Where(e => e.EndsWith(pattern));
        }

        /// <summary>
        /// Get all the files under a directory that optionally match a ending pattern
        /// </summary>
        /// <param name="baseDir">The directory under which to get files</param>
        /// <param name="pattern">An optional ending pattern to filter files on</param>
        /// <returns></returns>
        public IEnumerable<string> GetFiles(string baseDir, string pattern = null)
        {
            var output = new List<string>();
            foreach(var entry in _zip.Entries)
            {
                if (!entry.FullName.StartsWith(baseDir) || pattern != null && !entry.FullName.EndsWith(pattern))
                {
                    continue;
                }

                output.Add(entry.FullName);
            }
            return output;
        }

        /// <summary>
        /// Check whether or not a file exists
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>Whether or not the path exists</returns>
        public bool FileExists(string path)
        {
            return _zip.Entries.Any(e => e.FullName == path);
        }
        /// <summary>
        /// Join a path essentially just shorthand for JoinPath
        /// </summary>
        /// <param name="input">A list of paths components to join</param>
        /// <returns>The joined path</returns>
        public string Join(params string[] input)
        {
            return JoinPath(input);
        }

        /// <summary>
        /// Joins a group of path componenents together into a path inside of the zip
        /// </summary>
        /// <param name="input">A list of paths components to join</param>
        /// <returns>The joined path</returns>
        public string JoinPath(params string[] input)
        {
            return NormalizePath(string.Join(Seperater, input));
        }
        /// <summary>
        /// Normalize a path to remove .
        /// </summary>
        /// <param name="input">The string to normalize</param>
        /// <returns>The normalized string</returns>
        private string NormalizePath(string input)
        {
            //TODO: This should be modified to handle ".." also but that is slightly more difficult
            return string.Join(Seperater,input.Split(Seperater).Where(i => i != "."));
        }

        /// <summary>
        /// Get all the folders under a path
        /// </summary>
        /// <param name="path">The path to get folders under</param>
        /// <returns>An enumerable list of folders in this directory</returns>
        public IEnumerable<string> GetFolders(string path = null)
        {
            // folders have a compressed length of 0
            var output = _zip.Entries.Where(e => e.CompressedLength == 0).Select(e => e.FullName);
            if (path != null)
            {
                // this is a request for the top level directories
                output = output.Where(e => e.StartsWith(path)).Select(s => s.Substring(path.TrimEnd(Seperater).Length + 1)).Where( e => !string.IsNullOrEmpty(e));

            }

            return output 
                .Select( e=> e.Split(Seperater)[0])
                .Distinct();
        }

        /// <summary>
        /// Closes the zip file system and releases the lock on the underlying file
        /// </summary>
        public void Close()
        {
            _zip.Dispose();
            _stream.Close();
        }
    }
}
