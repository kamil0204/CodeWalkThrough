using System;
using System.IO;
using CodeWalkThrough.Managers;

// Path to the repository to analyze
string projectPath = @"C:\repos\applications.web.intel-foundry.ifs3.api-project";

// Path to store the LiteDB database
string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repository-graph.db");

// Path to export the repository structure
string exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repo-structure");
Directory.CreateDirectory(exportPath);

try
{
    Console.WriteLine($"Analyzing repository: {projectPath}");
    
    // Create the repository manager and analyze structure
    using var repoManager = new RepositoryManager(projectPath, dbPath);
    repoManager.AnalyzeAndStoreRepositoryStructure();
    
    // Export as Markdown
    var markdownPath = Path.Combine(exportPath, "repository-structure.md");
    repoManager.ExportAsMarkdownTree(markdownPath);
    
    Console.WriteLine("\nAnalysis completed successfully!");
    Console.WriteLine($"Markdown tree exported to: {markdownPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}


