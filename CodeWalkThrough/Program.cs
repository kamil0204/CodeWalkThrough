using System;
using System.IO;
using CodeWalkThrough.Managers;

// Path to the repository to analyze
string projectPath = @"C:\repos\applications.web.intel-foundry.ifs3.api-project";

// Path to store the databases
string liteDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repository-graph.db");
string liteGraphDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repository-graph-lite.db");

// Path to export the repository structure
string exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repo-structure");
Directory.CreateDirectory(exportPath);

try
{
    Console.WriteLine($"Analyzing repository: {projectPath}");
    
    // First run with LiteDB (default)
    Console.WriteLine("\n=== Using LiteDB Implementation ===");
    using (var repoManager = new RepositoryManager(projectPath, liteDbPath, false))
    {
        repoManager.AnalyzeAndStoreRepositoryStructure();
        
        // Export as Markdown
        var markdownPath = Path.Combine(exportPath, "repository-structure-litedb.md");
        repoManager.ExportAsMarkdownTree(markdownPath);
        
        Console.WriteLine("LiteDB Analysis completed successfully!");
        Console.WriteLine($"Markdown tree exported to: {markdownPath}");
    }
    
    // Then run with LiteGraph
    Console.WriteLine("\n=== Using LiteGraph Implementation ===");
    using (var repoManager = new RepositoryManager(projectPath, liteGraphDbPath, true))
    {
        repoManager.AnalyzeAndStoreRepositoryStructure();
        
        // Export as Markdown
        var markdownPath = Path.Combine(exportPath, "repository-structure-litegraph.md");
        repoManager.ExportAsMarkdownTree(markdownPath);
        
        Console.WriteLine("LiteGraph Analysis completed successfully!");
        Console.WriteLine($"Markdown tree exported to: {markdownPath}");
    }
    
    Console.WriteLine("\nBoth implementations have been tested successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}


