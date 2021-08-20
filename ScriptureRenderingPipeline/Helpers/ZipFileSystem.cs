using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ScriptureRenderingPipeline.Helpers
{
    public class ZipFileSystem
    {
        private const char Seperater = '/';
        ZipArchive _zip;
        Stream _stream;
        public ZipFileSystem(string path)
        {
            _stream = File.OpenRead(path);
            _zip = new ZipArchive(_stream);
        }

        public string ReadAllText(string file)
        {
            using (StreamReader reader = new StreamReader(_zip.GetEntry(file).Open()))
            {
                return reader.ReadToEnd();
            }
        }

        public IEnumerable<string> GetAllFiles(string pattern = null)
        {
            if (pattern == null)
            {
                return _zip.Entries.Select(e => e.FullName);
            }

            return _zip.Entries.Select(e => e.FullName).Where(e => e.EndsWith(pattern));
        }
        public IEnumerable<string> GetFiles(string baseDir, string pattern = null)
        {
            if (pattern == null)
            {
                return _zip.Entries.Select(e => e.FullName).Where(e => e.StartsWith(baseDir));
            }

            return _zip.Entries.Select(e => e.FullName).Where(e => e.StartsWith(baseDir) && e.EndsWith(pattern));
        }

        public bool FileExists(string path)
        {
            return _zip.Entries.Any(e => e.FullName == path);
        }
        public string Join(params string[] input)
        {
            return JoinPath(input);
        }
        public string JoinPath(params string[] input)
        {
            return NormalizePath(string.Join(Seperater, input));
        }
        private string NormalizePath(string input)
        {
            return string.Join(Seperater,input.Split(Seperater).Where(i => i != "."));
        }
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
        public void Close()
        {
            _zip.Dispose();
            _stream.Close();
        }
    }
}
