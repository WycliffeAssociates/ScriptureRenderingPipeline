using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers
{
    /// <summary>
    /// A virtual file system that runs out of a zip file without extracting it
    /// </summary>
    public class ZipFileSystem : IZipFileSystem, IDisposable
    {
        private const char Separator = '/';
        readonly ZipArchive _zip;
        readonly Stream _stream;
        readonly ReadOnlyCollection<string> _allFiles;
        readonly PathIndex _files;
        readonly PathIndex _folders;
        readonly ReadOnlyCollection<string> _topLevelFolders;
        /// <summary>
        /// Create a new ZipFileSystem from a path
        /// </summary>
        /// <param name="path">The path of the zip file to load</param>
        public ZipFileSystem(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Zip file not found: {path}");
            _stream = File.OpenRead(path);
            if (_stream.Length == 0)
                throw new InvalidDataException($"Zip file is empty: {path}");
            try
            {
                _zip = new ZipArchive(_stream);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Failed to open zip file '{path}': {ex.Message}", ex);
            }
            (_allFiles, _files, _folders, _topLevelFolders) = BuildIndex(_zip);
        }
        /// <summary>
        /// Create a new ZipFileSystem from a Stream
        /// </summary>
        /// <param name="stream">The stream to use for loading from</param>
        public ZipFileSystem(Stream stream)
        {
            _stream = stream;
            if (_stream.Length == 0)
                throw new InvalidDataException("Zip stream is empty.");
            try
            {
                _zip = new ZipArchive(_stream);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Failed to open zip stream: {ex.Message}", ex);
            }
            (_allFiles, _files, _folders, _topLevelFolders) = BuildIndex(_zip);
        }

        /// <summary>
        /// Index the entries once so lookups don't have to scan the whole zip
        /// </summary>
        /// <param name="zip">The zip to index</param>
        /// <returns>All files in zip order, indexes of files and folders, and the top level folders in zip order</returns>
        private static (ReadOnlyCollection<string>, PathIndex, PathIndex, ReadOnlyCollection<string>) BuildIndex(ZipArchive zip)
        {
            var files = new List<(string Path, int Order)>();
            var folders = new List<(string Path, int Order)>();
            var order = 0;
            foreach (var entry in zip.Entries)
            {
                // folders have a compressed length of 0
                (entry.CompressedLength == 0 ? folders : files).Add((entry.FullName, order++));
            }
            return (Array.AsReadOnly(files.Select(f => f.Path).ToArray()),
                new PathIndex(files),
                new PathIndex(folders),
                Array.AsReadOnly(folders.Select(f => f.Path.Split(Separator)[0]).Distinct().ToArray()));
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
        /// Read all the text from a file in the zip
        /// </summary>
        /// <param name="file">An absolute path to the file in the zip</param>
        /// <returns>The contents of the file</returns>
        public async Task<string> ReadAllTextAsync(string file)
        {
            using (StreamReader reader = new StreamReader(_zip.GetEntry(file).Open()))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Get all the files in the zip that optionally match a ending pattern
        /// </summary>
        /// <param name="pattern">An optional ending pattern to match</param>
        /// <returns>A Enumberable of absolute paths to files</returns>
        public IEnumerable<string> GetAllFiles(string? pattern = null)
        {
            if (pattern == null)
            {
                return _allFiles;
            }

            return _allFiles.Where(e => e.EndsWith(pattern, StringComparison.Ordinal));
        }

        /// <summary>
        /// Get all the files under a directory that optionally match a ending pattern
        /// </summary>
        /// <param name="baseDir">The directory under which to get files</param>
        /// <param name="pattern">An optional ending pattern to filter files on</param>
        /// <returns></returns>
        public IEnumerable<string> GetFiles(string baseDir, string pattern = null)
        {
            var matches = _files.StartingWith(AsDirectoryPrefix(baseDir));
            if (pattern != null)
            {
                matches = matches.Where(e => e.EndsWith(pattern, StringComparison.Ordinal));
            }
            return matches.ToList();
        }

        /// <summary>
        /// Check whether a file exists
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>Whether the path exists</returns>
        public bool FileExists(string path)
        {
            // ZipArchive already keeps a dictionary of entries by name
            return _zip.GetEntry(path) != null;
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
            return NormalizePath(string.Join(Separator, input));
        }
        /// <summary>
        /// Normalize a path to remove . and ..
        /// </summary>
        /// <param name="input">The string to normalize</param>
        /// <returns>The normalized string</returns>
        private string NormalizePath(string input)
        {
            // Remove '.' and resolve '..' using a stack, as is proper in modern C#.
            var stack = new Stack<string>();
            foreach (var part in input.Split(Separator))
            {
                if (part == "." || string.IsNullOrEmpty(part))
                    continue;
                if (part == "..")
                {
                    if (stack.Count > 0)
                        stack.Pop();
                    continue;
                }
                stack.Push(part);
            }
            return string.Join(Separator, stack.Reverse());
        }

        /// <summary>
        /// Get all the folders under a path
        /// </summary>
        /// <param name="path">The path to get folders under</param>
        /// <returns>An enumerable list of folders in this directory</returns>
        public IEnumerable<string> GetFolders(string? path = null)
        {
            // this is a request for the top level directories
            if (path == null)
            {
                return _topLevelFolders;
            }

            var prefix = AsDirectoryPrefix(path);
            return _folders.StartingWith(prefix)
                .Where(e => e.Length > prefix.Length)
                .Select(e => e[prefix.Length..].Split(Separator)[0])
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct();
        }

        /// <summary>
        /// Turn a directory path into a prefix that only matches things inside that directory,
        /// so that "gen/1" doesn't also match "gen/10"
        /// </summary>
        /// <param name="path">The directory path, with or without a trailing separator</param>
        /// <returns>The path ending in a separator, or an empty string for the root</returns>
        private static string AsDirectoryPrefix(string path)
        {
            var trimmed = path.TrimEnd(Separator);
            return trimmed.Length == 0 ? string.Empty : trimmed + Separator;
        }

        /// <summary>
        /// Closes the zip file system and releases the lock on the underlying file
        /// </summary>
        public void Close()
        {
            _zip.Dispose();
            _stream.Close();
        }

        public void Dispose()
        {
            Close();
        }

        public Stream GetStream()
        {
            var copy = new MemoryStream();
            _stream.Position = 0;
            _stream.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }

        /// <summary>
        /// Paths sorted ordinally so everything under a prefix is one contiguous run found by binary search.
        /// Each path keeps its position in the zip so results come back in the same order as a full scan.
        /// </summary>
        private sealed class PathIndex
        {
            private readonly (string Path, int Order)[] _sorted;

            public PathIndex(List<(string Path, int Order)> items)
            {
                _sorted = items.ToArray();
                Array.Sort(_sorted, (a, b) => string.CompareOrdinal(a.Path, b.Path));
            }

            public IEnumerable<string> StartingWith(string prefix)
            {
                int start = 0, high = _sorted.Length;
                while (start < high)
                {
                    var mid = start + (high - start) / 2;
                    if (string.CompareOrdinal(_sorted[mid].Path, prefix) < 0)
                    {
                        start = mid + 1;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                var end = start;
                while (end < _sorted.Length && _sorted[end].Path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    end++;
                }

                return _sorted[start..end].OrderBy(i => i.Order).Select(i => i.Path);
            }
        }
    }
}
