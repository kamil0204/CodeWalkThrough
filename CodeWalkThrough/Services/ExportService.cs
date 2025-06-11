using System;
using System.IO;
using System.Text;
using CodeWalkThrough.Models;

namespace CodeWalkThrough.Services
{    /// <summary>
    /// Service for exporting repository structure in various formats
    /// </summary>
    public class ExportService
    {
        private readonly IGraphDatabaseService _databaseService;
          /// <summary>
        /// Creates a new instance of ExportService with a database service
        /// </summary>
        /// <param name="databaseService">The database service to use for node retrieval</param>
        public ExportService(IGraphDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }
          /// <summary>
        /// Exports the repository structure as a Markdown tree
        /// </summary>
        /// <returns>A string containing the Markdown representation</returns>
        public string ExportAsMarkdownTree()
        {
            var rootNode = _databaseService.GetRootNode();
            if (rootNode == null)
                throw new InvalidOperationException("No root node found in the database");
            
            var markdown = new StringBuilder();
            markdown.AppendLine("# Repository Structure");
            markdown.AppendLine();
            
            ExportNodeToMarkdown(rootNode, markdown, 0);
            
            return markdown.ToString();
        }          /// <summary>
        /// Recursive method to export a node and its children to Markdown
        /// </summary>
        private void ExportNodeToMarkdown(FileSystemNode node, StringBuilder markdown, int depth)
        {
            string indent = new string(' ', depth * 2);
            string nodeType = node.IsDirectory ? "📁" : "📄";
            
            markdown.AppendLine($"{indent}- {nodeType} **{node.Name}**");
            
            foreach (var childId in node.Children)
            {
                var childNode = _databaseService.GetNodeById(childId);
                if (childNode != null)
                    ExportNodeToMarkdown(childNode, markdown, depth + 1);
            }
        }
        
        /// <summary>
        /// Saves content to a file
        /// </summary>
        /// <param name="content">The content to save</param>
        /// <param name="filePath">The path where to save the file</param>
        public void SaveToFile(string content, string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }
}