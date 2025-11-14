using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Helper methods for file and directory operations
    /// </summary>
    public static class FileHelpers
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region File Existence and Validation

        /// <summary>
        /// Checks if a file exists and is accessible
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if file exists and is accessible</returns>
        public static bool FileExistsAndAccessible(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to check if file exists: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Validates that a file exists and throws a descriptive exception if not
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="fileDescription">Human-readable description of the file type</param>
        /// <exception cref="FileNotFoundException">If the file does not exist</exception>
        public static void ValidateFileExists(string filePath, string fileDescription = "File")
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException($"{fileDescription} path cannot be null or empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"{fileDescription} not found: {filePath}", filePath);
            }
        }

        /// <summary>
        /// Checks if a directory exists and is accessible
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <returns>True if directory exists and is accessible</returns>
        public static bool DirectoryExistsAndAccessible(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return false;

            try
            {
                return Directory.Exists(directoryPath);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to check if directory exists: {directoryPath}");
                return false;
            }
        }

        #endregion

        #region File Locking

        /// <summary>
        /// Checks if a file is currently locked by another process
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if file is locked, false if accessible or doesn't exist</returns>
        public static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Unexpected error checking if file is locked: {filePath}");
                return true; // Assume locked if we can't determine
            }
        }

        /// <summary>
        /// Waits for a file to become unlocked, with timeout
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="maxWaitMs">Maximum time to wait in milliseconds</param>
        /// <param name="checkIntervalMs">Interval between checks in milliseconds</param>
        /// <returns>True if file became unlocked within timeout</returns>
        public static bool WaitForFileUnlock(string filePath, int maxWaitMs = 2000, int checkIntervalMs = 200)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var attempts = 0;

            while (IsFileLocked(filePath) && stopwatch.ElapsedMilliseconds < maxWaitMs)
            {
                attempts++;
                Logger.Debug($"File is locked, waiting... (attempt {attempts}): {filePath}");
                Thread.Sleep(checkIntervalMs);
            }

            var wasUnlocked = !IsFileLocked(filePath);
            if (wasUnlocked)
            {
                Logger.Debug($"File unlocked after {stopwatch.ElapsedMilliseconds}ms ({attempts} attempts)");
            }
            else
            {
                Logger.Warn($"File remained locked after {stopwatch.ElapsedMilliseconds}ms ({attempts} attempts): {filePath}");
            }

            return wasUnlocked;
        }

        #endregion

        #region Directory Operations

        /// <summary>
        /// Ensures a directory exists, creating it if necessary
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <returns>True if directory exists or was created successfully</returns>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                Logger.Warn("Cannot ensure directory exists - path is null or empty");
                return false;
            }

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Logger.Debug($"Created directory: {directoryPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to create directory: {directoryPath}");
                return false;
            }
        }

        /// <summary>
        /// Gets the directory path for a file, creating it if necessary
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Directory path, or null if failed</returns>
        public static string? EnsureDirectoryForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    EnsureDirectoryExists(directory);
                    return directory;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to ensure directory for file: {filePath}");
            }

            return null;
        }

        #endregion

        #region Path Operations

        /// <summary>
        /// Safely combines path segments, handling nulls and empty strings
        /// </summary>
        /// <param name="paths">Path segments to combine</param>
        /// <returns>Combined path, or empty string if all segments are empty</returns>
        public static string SafeCombinePaths(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return string.Empty;

            var validPaths = new System.Collections.Generic.List<string>();
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    validPaths.Add(path);
                }
            }

            if (validPaths.Count == 0)
                return string.Empty;

            try
            {
                return Path.Combine(validPaths.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to combine paths: {string.Join(", ", paths)}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the file extension without the leading dot
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Extension without dot, or empty string if no extension</returns>
        public static string GetExtensionWithoutDot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            try
            {
                var extension = Path.GetExtension(filePath);
                return extension.StartsWith(".") ? extension.Substring(1) : extension;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to get extension from path: {filePath}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if a file has a specific extension (case-insensitive)
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="extension">Extension to check (with or without leading dot)</param>
        /// <returns>True if file has the specified extension</returns>
        public static bool HasExtension(string filePath, string extension)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(extension))
                return false;

            try
            {
                var fileExt = Path.GetExtension(filePath).TrimStart('.');
                var checkExt = extension.TrimStart('.');
                return fileExt.Equals(checkExt, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to check extension for path: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Changes the extension of a file path
        /// </summary>
        /// <param name="filePath">Original file path</param>
        /// <param name="newExtension">New extension (with or without leading dot)</param>
        /// <returns>File path with new extension</returns>
        public static string ChangeExtension(string filePath, string newExtension)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return filePath;

            try
            {
                // Ensure extension has leading dot
                if (!string.IsNullOrWhiteSpace(newExtension) && !newExtension.StartsWith("."))
                {
                    newExtension = "." + newExtension;
                }

                return Path.ChangeExtension(filePath, newExtension);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to change extension for path: {filePath}");
                return filePath;
            }
        }

        #endregion

        #region File Size and Info

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>File size in bytes, or -1 if file doesn't exist or error occurred</returns>
        public static long GetFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to get file size: {filePath}");
            }

            return -1;
        }

        /// <summary>
        /// Formats a file size in human-readable format (reuses AudioHelpers.FormatAudioSize)
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        public static string FormatFileSize(long bytes) => AudioHelpers.FormatAudioSize(bytes);

        /// <summary>
        /// Gets formatted file size for a specific file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Formatted size string, or "Unknown" if file doesn't exist</returns>
        public static string GetFormattedFileSize(string filePath)
        {
            var size = GetFileSize(filePath);
            return size >= 0 ? FormatFileSize(size) : "Unknown";
        }

        #endregion

        #region Backup and Temp Files

        /// <summary>
        /// Creates a backup copy of a file with .bak extension
        /// </summary>
        /// <param name="filePath">Path to the file to backup</param>
        /// <param name="overwrite">Whether to overwrite existing backup</param>
        /// <returns>Path to backup file, or null if backup failed</returns>
        public static string? CreateBackup(string filePath, bool overwrite = true)
        {
            if (!File.Exists(filePath))
            {
                Logger.Warn($"Cannot create backup - file does not exist: {filePath}");
                return null;
            }

            try
            {
                var backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite);
                Logger.Info($"Created backup: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to create backup of: {filePath}");
                return null;
            }
        }

        /// <summary>
        /// Generates a unique temporary file path with a specific extension
        /// </summary>
        /// <param name="extension">File extension (with or without leading dot)</param>
        /// <param name="prefix">Optional filename prefix</param>
        /// <returns>Path to temporary file</returns>
        public static string GetTempFilePath(string extension = ".tmp", string prefix = "temp")
        {
            var ext = extension.StartsWith(".") ? extension : "." + extension;
            var fileName = $"{prefix}_{Guid.NewGuid():N}{ext}";
            return Path.Combine(Path.GetTempPath(), fileName);
        }

        #endregion

        #region Safe File Operations

        /// <summary>
        /// Safely deletes a file, ignoring errors
        /// </summary>
        /// <param name="filePath">Path to the file to delete</param>
        /// <returns>True if file was deleted or doesn't exist</returns>
        public static bool SafeDeleteFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Debug($"Deleted file: {filePath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete file: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Safely reads all text from a file, returning empty string on error
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>File contents, or empty string if error occurred</returns>
        public static string SafeReadAllText(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to read file: {filePath}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Safely writes text to a file, creating directory if needed
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="content">Content to write</param>
        /// <returns>True if write succeeded</returns>
        public static bool SafeWriteAllText(string filePath, string content)
        {
            try
            {
                EnsureDirectoryForFile(filePath);
                File.WriteAllText(filePath, content);
                Logger.Debug($"Wrote to file: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to write to file: {filePath}");
                return false;
            }
        }

        #endregion
    }
}
