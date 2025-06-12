using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using CodeWalkThrough.Models;

namespace CodeWalkThrough.Services
{
    /// <summary>
    /// Service for parsing repository structure from markdown files
    /// </summary>
    public class MarkdownParserService
    {
        private readonly string _markdownPath;

        /// <summary>
        /// Initializes a new instance of the MarkdownParserService
        /// </summary>
        /// <param name="markdownFilePath">Path to the markdown file containing repository structure</param>
        public MarkdownParserService(string markdownFilePath)
        {
            _markdownPath = markdownFilePath;
        }

        /// <summary>
        /// Parses the markdown file and returns a dictionary of file paths
        /// </summary>
        public Dictionary<string, string> ParseRepositoryStructure()
        {
            var result = new Dictionary<string, string>();
            
            if (!File.Exists(_markdownPath))
            {
                throw new FileNotFoundException($"Markdown file not found: {_markdownPath}");
            }

            string content = File.ReadAllText(_markdownPath);
            
            // Extract file paths from the markdown using regex
            // Looking for patterns like: "- 📄 **filename.ext**"
            var filePattern = new Regex(@"- 📄 \*\*(.*?)\*\*");
            var matches = filePattern.Matches(content);

            string currentPath = string.Empty;
            
            // Process all lines to build paths
            var lines = content.Split('\n');
            var indentStack = new Stack<(int level, string path)>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                    
                int indentLevel = line.TakeWhile(c => c == ' ').Count();
                
                // Handle directory entries
                if (line.Contains("📁") && line.Contains("**"))
                {
                    var dirMatch = Regex.Match(line, @"- 📁 \*\*(.*?)\*\*");
                    if (dirMatch.Success)
                    {
                        string dirName = dirMatch.Groups[1].Value;
                        
                        // Pop stack items with higher or equal indent level
                        while (indentStack.Count > 0 && indentStack.Peek().level >= indentLevel)
                            indentStack.Pop();
                            
                        string parentPath = indentStack.Count > 0 ? indentStack.Peek().path : string.Empty;
                        string fullPath = string.IsNullOrEmpty(parentPath) ? dirName : Path.Combine(parentPath, dirName);
                        
                        indentStack.Push((indentLevel, fullPath));
                    }
                }
                // Handle file entries
                else if (line.Contains("📄") && line.Contains("**"))
                {
                    var fileMatch = Regex.Match(line, @"- 📄 \*\*(.*?)\*\*");
                    if (fileMatch.Success)
                    {
                        string fileName = fileMatch.Groups[1].Value;
                        
                        // Use the parent directory from the stack
                        string parentPath = indentStack.Count > 0 ? indentStack.Peek().path : string.Empty;
                        string fullPath = string.IsNullOrEmpty(parentPath) ? fileName : Path.Combine(parentPath, fileName);
                        
                        // Add to the result dictionary
                        result[fileName] = fullPath;
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Gets all controller files from the repository structure
        /// </summary>
        public List<string> GetControllerFiles()
        {
            var allFiles = ParseRepositoryStructure();
            var controllerFiles = new List<string>();
            
            foreach (var file in allFiles)
            {
                if (file.Key.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))
                {
                    controllerFiles.Add(file.Value);
                }
            }
            
            return controllerFiles;
        }
    }
}
