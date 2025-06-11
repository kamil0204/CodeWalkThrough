using System;
using System.Collections.Generic;
using System.IO;
using CodeWalkThrough.Models;
using CodeWalkThrough.Services;

namespace CodeWalkThrough.Managers
{
    /// <summary>
    /// Manager for handling repository code analysis operations
    /// </summary>
    public class RepositoryManager : IDisposable
    {
        private readonly FileSystemService _fileSystemService;
        private readonly IGraphDatabaseService _databaseService;
        
        /// <summary>
        /// Creates a new instance of the RepositoryManager
        /// </summary>
        /// <param name="repositoryPath">Path to the Git repository</param>
        /// <param name="dbPath">Path to the database</param>
        public RepositoryManager(string repositoryPath, string dbPath)
        {
            _fileSystemService = new FileSystemService(repositoryPath);
            _databaseService = new LiteDbGraphDatabaseService(dbPath);
        }
        /// <summary>
        /// Analyzes the repository structure and stores it in the database
        /// </summary>
        public void AnalyzeAndStoreRepositoryStructure()
        {
            // Read the repository structure
            var rootNode = _fileSystemService.ReadRepositoryStructure();
            var allNodes = _fileSystemService.GetAllNodes();
            
            // Store the structure in the database
            _databaseService.StoreFileSystemTree(allNodes);
        }        /// <summary>
        /// Prints the repository file structure to the console
        /// </summary>
        public void PrintFileStructure()
        {
            var rootNode = _databaseService.GetRootNode();
            PrintNode(rootNode, 0);
        }

        /// <summary>
        /// Helper method to print a node and its children recursively
        /// </summary>
        /// <param name="node">The node to print</param>
        /// <param name="depth">Current depth in the tree</param>
        private void PrintNode(FileSystemNode node, int depth)
        {
            var indent = new string(' ', depth * 2);
            string displayName = node.IsDirectory ? $"{node.Name}/" : node.Name;
            Console.WriteLine($"{indent}{displayName}");
            
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var childId in node.Children)
                {
                    var childNode = _databaseService.GetNodeById(childId);
                    if (childNode != null)
                    {
                        PrintNode(childNode, depth + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Exports the repository structure as a Markdown tree
        /// </summary>
        /// <param name="outputPath">Path to save the markdown file</param>
        public void ExportAsMarkdownTree(string outputPath)
        {
            var exportService = new ExportService(_databaseService);
            var markdownContent = exportService.ExportAsMarkdownTree();
            exportService.SaveToFile(markdownContent, outputPath);
        }

        /// <summary>
        /// Disposes the resources used by this manager
        /// </summary>
        public void Dispose()
        {
            _databaseService.Dispose();
        }
    }
}