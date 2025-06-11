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
            
            try
            {
                // Handle file extensions (*.ext patterns)
                if (pattern.StartsWith("*."))
                {
                    string extension = pattern.Substring(1); // Include the dot
                    _standardIgnorePatterns.Add(extension);
                    return;
                }
                
                // Remove trailing slashes for directory patterns
                if (pattern.EndsWith("/"))
                {
                    pattern = pattern.TrimEnd('/');
                    if (!string.IsNullOrEmpty(pattern))
                        _standardIgnorePatterns.Add(pattern);
                    return;
                }
                
                // Handle specific path patterns with subdirectories
                if (pattern.Contains("/"))
                {
                    // Extract file/directory name from path
                    string[] segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0)
                    {
                        // Add the last segment (file/directory name)
                        _standardIgnorePatterns.Add(segments[segments.Length - 1]);
                    }
                    return;
                }
                
                // Simple patterns (like 'bin', 'obj', etc.)
                if (!string.IsNullOrEmpty(pattern))
                {
                    _standardIgnorePatterns.Add(pattern);
                }
            }
            catch
            {
                // Silently continue if pattern processing fails
            }
        }/// <summary>
        /// Determines if a path should be ignored based on .gitignore patterns
        /// </summary>
        private bool ShouldIgnore(string path)
        {
            // Never ignore the root path
            if (string.Equals(path, _rootPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string relativePath = Path.GetRelativePath(_rootPath, path).Replace('\\', '/');
            
            // Always ignore .git directory and its contents
            if (relativePath.Contains(".git/") || relativePath.Equals(".git", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Use git's ignore mechanism if available
            if (Repository.IsValid(_rootPath))
            {
                try
                {
                    using var repo = new Repository(_rootPath);
                    return repo.Ignore.IsPathIgnored(relativePath);
                }
                catch
                {
                    // Silently continue with fallback pattern matching
                }
            }
            
            // Simple pattern matching from our stored ignore patterns
            string fileName = Path.GetFileName(path).ToLowerInvariant();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            
            // Check for extension or filename matches
            if (_standardIgnorePatterns.Contains(ext) || _standardIgnorePatterns.Contains(fileName))
                return true;

            // Check path segments
            string[] pathParts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in pathParts)
            {
                if (_standardIgnorePatterns.Contains(part.ToLowerInvariant()))
                    return true;
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
