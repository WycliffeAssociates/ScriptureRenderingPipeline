using System.Collections.Generic;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers;

public interface IZipFileSystem
{
    /// <summary>
    /// Read all the text from a file in the zip
    /// </summary>
    /// <param name="file">An absolute path to the file in the zip</param>
    /// <returns>The contents of the file</returns>
    string ReadAllText(string file);

    /// <summary>
    /// Read all the text from a file in the zip
    /// </summary>
    /// <param name="file">An absolute path to the file in the zip</param>
    /// <returns>The contents of the file</returns>
    Task<string> ReadAllTextAsync(string file);

    /// <summary>
    /// Get all the files in the zip that optionally match a ending pattern
    /// </summary>
    /// <param name="pattern">An optional ending pattern to match</param>
    /// <returns>A Enumberable of absolute paths to files</returns>
    IEnumerable<string> GetAllFiles(string pattern = null);

    /// <summary>
    /// Get all the files under a directory that optionally match a ending pattern
    /// </summary>
    /// <param name="baseDir">The directory under which to get files</param>
    /// <param name="pattern">An optional ending pattern to filter files on</param>
    /// <returns></returns>
    IEnumerable<string> GetFiles(string baseDir, string pattern = null);

    /// <summary>
    /// Check whether or not a file exists
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>Whether or not the path exists</returns>
    bool FileExists(string path);

    /// <summary>
    /// Join a path essentially just shorthand for JoinPath
    /// </summary>
    /// <param name="input">A list of paths components to join</param>
    /// <returns>The joined path</returns>
    string Join(params string[] input);

    /// <summary>
    /// Joins a group of path componenents together into a path inside of the zip
    /// </summary>
    /// <param name="input">A list of paths components to join</param>
    /// <returns>The joined path</returns>
    string JoinPath(params string[] input);

    /// <summary>
    /// Get all the folders under a path
    /// </summary>
    /// <param name="path">The path to get folders under</param>
    /// <returns>An enumerable list of folders in this directory</returns>
    IEnumerable<string> GetFolders(string path = null);

    /// <summary>
    /// Closes the zip file system and releases the lock on the underlying file
    /// </summary>
    void Close();
}