using System;
using System.Collections.Generic;
using System.IO;
using CodeWalkThrough.Models;
using LibGit2Sharp;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// Service for reading the file system structure respecting .gitignore rules
    /// </summary>
    public class FileSystemService
    {
        private readonly string _rootPath;
        private readonly Dictionary<string, FileSystemNode> _nodesDict = new Dictionary<string, FileSystemNode>();
        private readonly HashSet<string> _standardIgnorePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FileSystemService(string rootPath)
        {
            _rootPath = Path.GetFullPath(rootPath);
        }/// <summary>
        /// Reads the repository structure respecting .gitignore rules
        /// </summary>
        /// <returns>The root node of the file system tree</returns>
        public FileSystemNode ReadRepositoryStructure()
        {
            _nodesDict.Clear();
            _standardIgnorePatterns.Clear();

            // Load ignore patterns from .gitignore file
            InitializeStandardIgnorePatterns();

            // Create the root node
            var rootInfo = new DirectoryInfo(_rootPath);
            var rootNode = new FileSystemNode
            {
                Path = _rootPath,
                Name = rootInfo.Name,
                IsDirectory = true,
                LastModified = rootInfo.LastWriteTime,
                ParentId = null
            };

            _nodesDict[rootNode.Id] = rootNode;

            // Process the entire directory tree
            ProcessDirectory(rootInfo, rootNode);

            return rootNode;
        }

        /// <summary>
        /// Gets all file system nodes
        /// </summary>
        public IEnumerable<FileSystemNode> GetAllNodes()
        {
            return _nodesDict.Values;
        }        private void InitializeStandardIgnorePatterns()
        {
            // Clear any existing patterns
            _standardIgnorePatterns.Clear();
            
            // Always add .git directory as critical pattern - this should always be ignored regardless of .gitignore
            _standardIgnorePatterns.Add(".git");
            
            // Try to read from .gitignore file in the project root
            string gitIgnorePath = Path.Combine(_rootPath, ".gitignore");
            
            if (File.Exists(gitIgnorePath))
            {
                try
                {
                    Console.WriteLine($"Reading ignore patterns from .gitignore file: {gitIgnorePath}");
                    string[] lines = File.ReadAllLines(gitIgnorePath);
                    int patternCount = 0;
                    
                    foreach (string line in lines)
                    {
                        // Skip comments and empty lines
                        string trimmedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                            continue;
                        
                        // Handle negation patterns (patterns that start with !)
                        if (trimmedLine.StartsWith("!"))
                            continue; // Simply skip negation patterns for now
                            
                        // Process different pattern types
                        ProcessGitIgnorePattern(trimmedLine);
                        patternCount++;
                    }
                    
                    Console.WriteLine($"Successfully loaded {patternCount} ignore patterns from .gitignore");
                    
                    // If no patterns were loaded from .gitignore, load some common default patterns
                    // as a fallback to ensure build artifacts don't get included
                    if (patternCount == 0)
                    {
                        Console.WriteLine("Warning: No patterns were loaded from .gitignore file. Adding common build artifact patterns as fallback.");
                        AddCommonBuildArtifactPatterns();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading .gitignore file: {ex.Message}");
                    Console.WriteLine("Adding common build artifact patterns as fallback.");
                    AddCommonBuildArtifactPatterns();
                }
            }
            else
            {
                Console.WriteLine($"No .gitignore file found at {gitIgnorePath}");
                Console.WriteLine("Adding common build artifact patterns as fallback.");
                AddCommonBuildArtifactPatterns();
            }
        }
        
        /// <summary>
        /// Adds common build artifact patterns as fallback when no .gitignore is found
        /// </summary>
        private void AddCommonBuildArtifactPatterns()
        {
            // Common build directories
            _standardIgnorePatterns.Add("build");
            _standardIgnorePatterns.Add("dist");
            _standardIgnorePatterns.Add("out");
            _standardIgnorePatterns.Add("target");
            _standardIgnorePatterns.Add("node_modules");
            _standardIgnorePatterns.Add("packages");
            
            // Common build file extensions
            _standardIgnorePatterns.Add(".cache");
        }        /// <summary>
        /// Process a gitignore pattern to extract meaningful ignore rules
        /// </summary>
        private void ProcessGitIgnorePattern(string pattern)
        {
            // Skip empty patterns
            if (string.IsNullOrWhiteSpace(pattern))
                return;
            
            // Store the original pattern for debugging
            string originalPattern = pattern;
            
            try
            {
                // Handle special patterns with **/ prefix (matches any directory depth)
                if (pattern.StartsWith("**/"))
                {
                    // Extract the part after **/ which can match at any directory depth
                    string patternAfterWildcard = pattern.Substring(3);
                    ProcessGitIgnorePattern(patternAfterWildcard); // Process the rest of the pattern
                    
                    // If the pattern after **/ is a directory (ends with slash)
                    if (patternAfterWildcard.EndsWith("/"))
                    {
                        string dirName = patternAfterWildcard.TrimEnd('/');
                        if (!string.IsNullOrEmpty(dirName) && !dirName.Contains("*"))
                            _standardIgnorePatterns.Add(dirName);
                    }
                    return;
                }
                
                // Remove trailing slashes for directory patterns
                if (pattern.EndsWith("/"))
                {
                    pattern = pattern.TrimEnd('/');
                    if (!string.IsNullOrEmpty(pattern) && !pattern.Contains("*"))
                        _standardIgnorePatterns.Add(pattern);
                    return;
                }
                
                // Handle file extensions (*.ext patterns)
                if (pattern.StartsWith("*."))
                {
                    string extension = pattern.Substring(1); // Include the dot
                    _standardIgnorePatterns.Add(extension);
                    return;
                }
                
                // Handle specific path patterns that might include subdirectories
                if (pattern.Contains("/"))
                {
                    // Extract all segments for better matching
                    string[] segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0)
                    {
                        // Add the last segment (file/directory name) for basic pattern matching
                        string lastSegment = segments[segments.Length - 1];
                        
                        // If the last segment is not a wildcard pattern, add it
                        if (!string.IsNullOrEmpty(lastSegment))
                        {
                            // Handle segment with wildcards
                            if (lastSegment.Contains("*"))
                            {
                                // Handle patterns like "prefix*.suffix"
                                int starIndex = lastSegment.IndexOf('*');
                                if (starIndex > 0)
                                {
                                    // Add the prefix for startsWith matching
                                    string prefix = lastSegment.Substring(0, starIndex);
                                    if (!string.IsNullOrEmpty(prefix))
                                        _standardIgnorePatterns.Add($"prefix:{prefix}");
                                }
                                
                                // Handle patterns like "*.suffix" or "prefix*suffix"
                                int lastStarIndex = lastSegment.LastIndexOf('*');
                                if (lastStarIndex < lastSegment.Length - 1)
                                {
                                    string suffix = lastSegment.Substring(lastStarIndex + 1);
                                    if (!string.IsNullOrEmpty(suffix))
                                        _standardIgnorePatterns.Add($"suffix:{suffix}");
                                }
                                
                                // Try to extract meaningful parts without wildcards
                                string simplifiedSegment = lastSegment.Replace("*", "");
                                if (!string.IsNullOrEmpty(simplifiedSegment))
                                    _standardIgnorePatterns.Add(simplifiedSegment);
                            }
                            else
                            {
                                // Add exact name match
                                _standardIgnorePatterns.Add(lastSegment);
                            }
                        }
                        
                        // Also add intermediate directory names that should be ignored
                        for (int i = 0; i < segments.Length - 1; i++)
                        {
                            string segment = segments[i];
                            if (segment == "**" || segment.Contains("*"))
                                continue; // Skip special patterns
                                
                            // Add directory name as a pattern if it's not a wildcard
                            if (!string.IsNullOrEmpty(segment))
                                _standardIgnorePatterns.Add(segment);
                        }
                    }
                    return;
                }
                
                // Handle simple patterns (like 'bin', 'obj', etc.)
                if (!pattern.Contains("*") && !string.IsNullOrEmpty(pattern))
                {
                    _standardIgnorePatterns.Add(pattern);
                    return;
                }
                
                // Handle wildcard patterns (more robust handling)
                if (pattern.Contains("*"))
                {
                    // Handle patterns like "*.txt"
                    if (pattern.StartsWith("*") && !pattern.Contains("**"))
                    {
                        string suffix = pattern.Substring(1);
                        if (!string.IsNullOrEmpty(suffix))
                            _standardIgnorePatterns.Add($"suffix:{suffix}"); // Add suffix for checking endswith
                    }
                    
                    // Handle patterns like "prefix*"
                    if (pattern.EndsWith("*") && !pattern.Contains("**"))
                    {
                        string prefix = pattern.Substring(0, pattern.Length - 1);
                        if (!string.IsNullOrEmpty(prefix))
                            _standardIgnorePatterns.Add($"prefix:{prefix}"); // Add prefix for checking startswith
                    }
                    
                    // Handle patterns like "prefix*suffix"
                    if (pattern.Contains("*") && !pattern.StartsWith("*") && !pattern.EndsWith("*"))
                    {
                        int starIndex = pattern.IndexOf('*');
                        string prefix = pattern.Substring(0, starIndex);
                        string suffix = pattern.Substring(starIndex + 1);
                        
                        if (!string.IsNullOrEmpty(prefix))
                            _standardIgnorePatterns.Add($"prefix:{prefix}");
                            
                        if (!string.IsNullOrEmpty(suffix))
                            _standardIgnorePatterns.Add($"suffix:{suffix}");
                    }
                    
                    // Also add any non-wildcard parts as potential matches
                    string simplifiedPattern = pattern
                        .Replace("**", "")
                        .Replace("*", "");
                        
                    if (!string.IsNullOrEmpty(simplifiedPattern))
                        _standardIgnorePatterns.Add(simplifiedPattern);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to process gitignore pattern '{originalPattern}': {ex.Message}");
            }
        }        /// <summary>
        /// Determines if a path should be ignored based on .gitignore patterns
        /// </summary>
        private bool ShouldIgnore(string path)
        {
            // Never ignore the root path
            if (string.Equals(path, _rootPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string relativePath = Path.GetRelativePath(_rootPath, path).Replace('\\', '/');
            
            // First try to use git's ignore mechanism - this uses the real .gitignore processing logic
            // This is the most accurate way to match patterns since it uses Git's actual implementation
            if (Repository.IsValid(_rootPath))
            {
                try
                {
                    using var repo = new Repository(_rootPath);
                    if (repo.Ignore.IsPathIgnored(relativePath))
                        return true;
                }
                catch (Exception ex)
                {
                    // Log the error but continue with fallback pattern matching
                    Console.WriteLine($"Warning: Failed to use Git's ignore mechanism: {ex.Message}. Falling back to pattern matching.");
                }
            }
            
            // If Git's ignore mechanism is not available or fails,
            // then fall back to our pattern matching from the .gitignore file
            
            // Always ignore .git directory and its contents
            if (relativePath.Contains(".git/") || relativePath.Equals(".git", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Check for ignored directories in the path segments
            string[] pathParts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in pathParts)
            {
                string partLower = part.ToLowerInvariant();
                // Exact directory/file name match
                if (_standardIgnorePatterns.Contains(partLower))
                    return true;
                    
                // Check for extension matches in path segments
                foreach (var pattern in _standardIgnorePatterns)
                {
                    // Check for file extension patterns
                    if (pattern.StartsWith(".") && partLower.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                        
                    // Check for prefix patterns (prefix:something)
                    if (pattern.StartsWith("prefix:"))
                    {
                        string prefix = pattern.Substring(7);
                        if (partLower.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    
                    // Check for suffix patterns (suffix:something)
                    if (pattern.StartsWith("suffix:"))
                    {
                        string suffix = pattern.Substring(7);
                        if (partLower.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            
            // More specific checks based on whether this is a file or directory
            if (File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string fileName = Path.GetFileName(path).ToLowerInvariant();
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                
                // Check for extension patterns
                if (_standardIgnorePatterns.Contains(ext))
                    return true;
                
                // Check for exact filename matches
                if (_standardIgnorePatterns.Contains(fileName))
                    return true;
                
                // Check for pattern matches
                foreach (var pattern in _standardIgnorePatterns)
                {
                    // Skip directory patterns with path separators
                    if (pattern.Contains("/") || pattern.Contains("\\"))
                        continue;
                        
                    // Skip special markers
                    if (pattern.StartsWith("prefix:") || pattern.StartsWith("suffix:"))
                        continue;
                
                    // Check if the pattern is a suffix that matches the filename
                    // but is not a file extension
                    if (!pattern.StartsWith(".") && fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                        
                    // Check if the pattern is a partial match for the filename
                    if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase) && 
                        pattern.Length > 2) // Avoid short patterns that might cause false positives
                        return true;
                }
                
                // Check prefix/suffix patterns
                foreach (var pattern in _standardIgnorePatterns)
                {
                    if (pattern.StartsWith("prefix:"))
                    {
                        string prefix = pattern.Substring(7);
                        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                            fileNameWithoutExt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else if (pattern.StartsWith("suffix:"))
                    {
                        string suffix = pattern.Substring(7);
                        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                            fileNameWithoutExt.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            else if (Directory.Exists(path))
            {
                // Check directory name
                string dirName = Path.GetFileName(path).ToLowerInvariant();
                
                // Exact directory name match
                if (_standardIgnorePatterns.Contains(dirName))
                    return true;
                    
                // Check for prefix/suffix patterns
                foreach (var pattern in _standardIgnorePatterns)
                {
                    if (pattern.StartsWith("prefix:"))
                    {
                        string prefix = pattern.Substring(7);
                        if (dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else if (pattern.StartsWith("suffix:"))
                    {
                        string suffix = pattern.Substring(7);
                        if (dirName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    
                    // Check if directory name contains the pattern
                    // Skip path separators and special markers
                    if (!pattern.Contains("/") && !pattern.Contains("\\") &&
                        !pattern.StartsWith("prefix:") && !pattern.StartsWith("suffix:") &&
                        pattern.Length > 2) // Avoid short patterns that might cause false positives
                    {
                        if (dirName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
                
            return false;
        }private void ProcessDirectory(DirectoryInfo directory, FileSystemNode parentNode)
        {
            // Skip this directory if it should be ignored
            if (ShouldIgnore(directory.FullName))
                return;

            // Process all files in this directory
            foreach (var file in directory.GetFiles())
            {
                if (ShouldIgnore(file.FullName))
                    continue;

                var fileNode = new FileSystemNode
                {
                    Path = file.FullName,
                    Name = file.Name,
                    IsDirectory = false,
                    Size = file.Length,
                    LastModified = file.LastWriteTime,
                    ParentId = parentNode.Id
                };

                _nodesDict[fileNode.Id] = fileNode;
                parentNode.Children.Add(fileNode.Id);
            }

            // Process all subdirectories
            foreach (var subDir in directory.GetDirectories())
            {
                if (ShouldIgnore(subDir.FullName))
                    continue;

                var dirNode = new FileSystemNode
                {
                    Path = subDir.FullName,
                    Name = subDir.Name,
                    IsDirectory = true,
                    LastModified = subDir.LastWriteTime,
                    ParentId = parentNode.Id
                };

                _nodesDict[dirNode.Id] = dirNode;
                parentNode.Children.Add(dirNode.Id);

                // Recursively process the subdirectory
                ProcessDirectory(subDir, dirNode);
            }
        }
    }
}
