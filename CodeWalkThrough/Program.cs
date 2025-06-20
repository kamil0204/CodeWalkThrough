using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using CodeWalkThrough.Managers;
using CodeWalkThrough.Models;
using CodeWalkThrough.Agents;
using CodeWalkThrough.Services;
using System.Text.Json;
using System.Collections.Generic;

namespace CodeWalkThrough 
{
    /// <summary>
    /// Main application class for CodeWalkThrough
    /// </summary>
    public class CodeWalkThroughApp
    {
        // Path to the repository to analyze (should be made configurable)
        public string ProjectPath { get; set; } = @"C:\repos\applications.web.intel-foundry.ifs3.api-project";

        // Path to store the database
        public string LiteGraphDbPath { get; private set; }

        // Path to export the repository structure
        public string ExportPath { get; private set; }

        // Path to store the code review plans
        public string ReviewPlansPath { get; private set; }

        // Path to store file reviews
        public string FileReviewsPath { get; private set; }

        /// <summary>
        /// Initializes the application paths
        /// </summary>
        public CodeWalkThroughApp()
        {
            // Initialize paths
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            LiteGraphDbPath = Path.Combine(baseDir, "repository-graph-lite.db");
            ExportPath = Path.Combine(baseDir, "repo-structure");
            ReviewPlansPath = Path.Combine(baseDir, "code-review-plans");
            FileReviewsPath = Path.Combine(baseDir, "file-reviews");
            
            // Create necessary directories
            Directory.CreateDirectory(ExportPath);
            Directory.CreateDirectory(ReviewPlansPath);
            Directory.CreateDirectory(FileReviewsPath);
        }

        /// <summary>
        /// Main method to run the code walkthrough analysis
        /// </summary>
        public async Task RunCodeWalkThroughAsync()
        {
            try
            {
                Console.WriteLine($"Analyzing repository: {ProjectPath}");
                  // Run database analysis
                Console.WriteLine("\n=== Analyzing Repository Structure ===");
                using (var repoManager = new RepositoryManager(ProjectPath, LiteGraphDbPath))
                {
                    repoManager.AnalyzeAndStoreRepositoryStructure();
                    
                    // Export as Markdown
                    var markdownPath = Path.Combine(ExportPath, "repository-structure-litegraph.md");
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
                        var markdownPath = Path.Combine(ExportPath, "repository-structure-litegraph.md");
                        var plan = await codeReviewPlanner.CreateCodeReviewPlanAsync(markdownPath, projectDescription);
                        
                        // Display summary of the plan
                        Console.WriteLine("\nCode Review Plan Generated:");
                        Console.WriteLine($"Title: {plan.Title}");
                        Console.WriteLine($"Description: {plan.Description}");
                        
                        // Display identified tech stack
                        if (plan.TechStack != null && plan.TechStack.Count > 0)
                        {
                            Console.WriteLine("\nIdentified Tech Stack:");
                            Console.WriteLine($"  {string.Join(", ", plan.TechStack)}");
                        }
                        
                        // Display either categories or components depending on what's available
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
                        Console.WriteLine($"Plan saved to: {ReviewPlansPath}");
                        
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
                            }
                        }
                        
                        // Ask if the user wants to review the recommended entry point files
                        Console.WriteLine("\nWould you like to review the entry point files in detail? (Y/N)");
                        string reviewResponse = Console.ReadLine() ?? "N";
                        if (reviewResponse.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            await ReviewEntryPointFilesAsync(recommendations, markdownPath);
                        }
                        
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

        /// <summary>
        /// Reviews each of the recommended entry point files using the FileReviewAgent
        /// </summary>
        /// <param name="filePaths">List of file paths to review</param>
        /// <param name="repositoryStructurePath">Path to the repository structure markdown file</param>
        private async Task ReviewEntryPointFilesAsync(List<string> filePaths, string repositoryStructurePath)
        {
            Console.WriteLine("\n=== Reviewing Entry Point Files ===");
            
            try
            {
                // Create a FileReviewAgent instance
                var fileReviewAgent = new FileReviewAgent(repositoryStructurePath);
                
                // List to store all reviews
                var allReviews = new List<FileReview>();
                
                // Track the total number of methods found
                int totalMethods = 0;
                
                // Process each file
                foreach (string relativePath in filePaths)
                {        
                    // Fix path issues - normalize path separators and remove potential duplicate references to the project path
                    string cleanRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    if (cleanRelativePath.StartsWith(Path.GetFileName(ProjectPath)))
                    {
                        cleanRelativePath = cleanRelativePath.Substring(Path.GetFileName(ProjectPath).Length + 1);
                    }
                    
                    // Convert relative path to absolute path
                    string absolutePath = Path.Combine(ProjectPath, cleanRelativePath);
                    
                    Console.WriteLine($"\nAnalyzing file: {cleanRelativePath}");
                    Console.WriteLine("This may take a moment...");
                    
                    try
                    {
                        // Review the file
                        var review = await fileReviewAgent.ReviewFileAsync(absolutePath);
                        allReviews.Add(review);
                        
                        // Export the review to markdown
                        string markdownPath = await fileReviewAgent.ExportReviewToMarkdownAsync(review);
                        
                        // Update the total methods count
                        totalMethods += review.Methods.Count;
                        
                        // Display a summary of the review
                        Console.WriteLine($"Found {review.Methods.Count} methods in the file");
                        Console.WriteLine($"Methods list saved to: {markdownPath}");
                        
                        // If there are methods, list them
                        if (review.Methods.Count > 0)
                        {
                            Console.WriteLine("\nMethods identified:");
                            foreach (var method in review.Methods)
                            {
                                Console.WriteLine($"- {method.Name} (Source: {method.Source})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error analyzing file {relativePath}: {ex.Message}");
                    }
                }
                
                // Display summary
                Console.WriteLine("\n=== File Review Summary ===");
                Console.WriteLine($"Files reviewed: {allReviews.Count}");
                Console.WriteLine($"Total methods identified: {totalMethods}");
                Console.WriteLine($"Reviews saved to: {FileReviewsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in file review process: {ex.Message}");
            }
        }
    }

    // Program entry point
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var app = new CodeWalkThroughApp();
            await app.RunCodeWalkThroughAsync();
        }
    }
}
