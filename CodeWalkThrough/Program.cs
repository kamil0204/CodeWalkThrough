using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using CodeWalkThrough.Managers;
using CodeWalkThrough.Models;
using CodeWalkThrough.Agents;
using CodeWalkThrough.Services;
using System.Text.Json;

// Path to the repository to analyze
string projectPath = @"C:\repos\applications.web.intel-foundry.ifs3.api-project";

// Path to store the databases
string liteDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repository-graph.db");
string liteGraphDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repository-graph-lite.db");

// Path to export the repository structure
string exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repo-structure");
Directory.CreateDirectory(exportPath);

// Path to store the code review plans
string reviewPlansPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "code-review-plans");
Directory.CreateDirectory(reviewPlansPath);

async Task RunCodeWalkThroughAsync()
{
    try
    {
        Console.WriteLine($"Analyzing repository: {projectPath}");
        
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
          // Ask if the user wants to generate a code review plan
        Console.WriteLine("\nWould you like to generate a code review plan? (Y/N)");
        string response = Console.ReadLine() ?? "N";
        
        if (response.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\n=== Generating Code Review Plan ===");
            
            Console.WriteLine("Loading settings from appsettings.json...");
              try
            {
                // Create the code review planner agent using configuration from appsettings.json
                var codeReviewPlanner = new CodeReviewPlannerAgent();
                
                // Project description
                Console.WriteLine("Enter a brief description of the project (optional):");
                string projectDescription = Console.ReadLine() ?? "";
                
                // Generate the code review plan
                Console.WriteLine("Generating code review plan... This may take a moment.");
                var markdownPath = Path.Combine(exportPath, "repository-structure-litedb.md");
                var plan = await codeReviewPlanner.CreateCodeReviewPlanAsync(markdownPath, projectDescription);                // Display summary of the plan
                Console.WriteLine("\nCode Review Plan Generated:");
                Console.WriteLine($"Title: {plan.Title}");
                Console.WriteLine($"Description: {plan.Description}");
                
                // Display identified tech stack
                if (plan.TechStack != null && plan.TechStack.Count > 0)
                {
                    Console.WriteLine("\nIdentified Tech Stack:");
                    Console.WriteLine($"  {string.Join(", ", plan.TechStack)}");
                }                // Display either categories or components depending on what's available
                if (plan.Categories != null && plan.Categories.Count > 0)
                {
                    int totalFiles = plan.Categories.Sum(c => c.Files.Count);
                    Console.WriteLine($"Categories: {plan.Categories.Count}");
                    Console.WriteLine($"Files to review: {totalFiles}");
                }
                else if (plan.Components != null && plan.Components.Count > 0)
                {
                    Console.WriteLine($"Components to review: {plan.Components.Count}");
                }
                Console.WriteLine($"Plan saved to: {reviewPlansPath}");
                  
                // Export the plan to markdown for better readability
                string planMarkdownPath = await codeReviewPlanner.ExportPlanToMarkdownAsync(plan);
                Console.WriteLine($"Detailed markdown plan saved to: {planMarkdownPath}");
                
                // Display categories (new structure) or fall back to components (old structure)
                if (plan.Categories != null && plan.Categories.Count > 0)
                {
                    Console.WriteLine("\nCategories by priority:");
                    var prioritizedCategories = plan.Categories.OrderBy(c => c.Priority).ToList();
                    foreach (var category in prioritizedCategories)
                    {
                        Console.WriteLine($"- [Priority {category.Priority}] {category.Name}: {category.Files.Count} files");
                    }
                }
                else if (plan.Components != null && plan.Components.Count > 0)
                {
                    Console.WriteLine("\nComponents by priority:");
                    var prioritized = plan.Components.OrderBy(c => c.Priority).ToList();
                    foreach (var component in prioritized)
                    {
                        Console.WriteLine($"- [{component.Priority}] {component.Name} ({component.ComponentType}): {component.FocusAreas.Count} focus areas");
                    }
                }
                
                // Get and display recommended entry points
                var recommendations = codeReviewPlanner.GetRecommendedEntryPoints(plan);
                if (recommendations.Count > 0)
                {
                    Console.WriteLine("\nRecommended code review entry points:");
                    foreach (var path in recommendations)
                    {
                        Console.WriteLine($"- {path}");
                    }                }
                
                // Prompt to open the markdown file
                Console.WriteLine("\nWould you like to view the detailed markdown plan? (Y/N)");
                string viewResponse = Console.ReadLine() ?? "N";
                if (viewResponse.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Use process to open the markdown file with the default application
                        var process = new System.Diagnostics.Process();
                        process.StartInfo.FileName = planMarkdownPath;
                        process.StartInfo.UseShellExecute = true;
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not open the file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration or generating plan: {ex.Message}");
                Console.WriteLine("Please make sure your appsettings.json file contains valid settings.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

// Run the async method
await RunCodeWalkThroughAsync();


