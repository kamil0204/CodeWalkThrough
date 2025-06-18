using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.SemanticKernel;
using System.Text;
using System.Collections.Generic;
using CodeWalkThrough.Models;
using CodeWalkThrough.Services;

namespace CodeWalkThrough.Agents
{
    /// <summary>
    /// Agent that analyzes source code files to identify methods and their implementations
    /// </summary>
    public class FileReviewAgent
    {
        private readonly Kernel _kernel;
        private readonly string _outputDirectory;
        private readonly string _repositoryStructurePath;

        /// <summary>
        /// Initializes a new instance of the FileReviewAgent using the application configuration
        /// </summary>
        /// <param name="repositoryStructurePath">Path to the repository structure markdown file</param>
        public FileReviewAgent(string repositoryStructurePath)
        {
            // Load settings from appsettings.json
            var configService = ConfigurationService.Instance;
            var appSettings = configService.GetAppSettings();
            var kernelSettings = appSettings.SemanticKernel.OpenAI;
            
            // Create the kernel builder
            var builder = Kernel.CreateBuilder();
            
            // Add OpenAI text completion service
            builder.AddOpenAIChatCompletion(
                modelId: kernelSettings.ModelId,
                apiKey: kernelSettings.ApiKey,
                endpoint: new Uri(kernelSettings.Endpoint));
            
            // Build the kernel
            _kernel = builder.Build();
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file-reviews");
            _repositoryStructurePath = repositoryStructurePath;
            
            // Create the output directory if it doesn't exist
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }
        
        /// <summary>
        /// Initializes a new instance of the FileReviewAgent with explicit settings
        /// </summary>
        /// <param name="apiKey">API key for the LLM service</param>
        /// <param name="endpoint">API endpoint</param>
        /// <param name="modelId">Model ID</param>
        /// <param name="outputDirectory">Directory where the file reviews will be saved</param>
        /// <param name="repositoryStructurePath">Path to the repository structure markdown file</param>
        public FileReviewAgent(string apiKey, string endpoint, string modelId, string outputDirectory, string repositoryStructurePath)
        {
            // Create the kernel builder
            var builder = Kernel.CreateBuilder();
            
            // Add OpenAI text completion service
            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                endpoint: new Uri(endpoint));
            
            // Build the kernel
            _kernel = builder.Build();
            _outputDirectory = outputDirectory;
            _repositoryStructurePath = repositoryStructurePath;
            
            // Create the output directory if it doesn't exist
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }        /// <summary>
        /// Reviews a source code file to identify methods and their implementations
        /// </summary>
        /// <param name="filePath">Path to the file to review</param>
        /// <returns>A FileReview object containing the analysis results</returns>
        public async Task<FileReview> ReviewFileAsync(string filePath)
        {
            // Normalize the file path - first replace any forward slashes with the OS-specific separator
            string normalizedPath = filePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            // Then get the full path to handle relative paths
            normalizedPath = Path.GetFullPath(normalizedPath);
              // Make sure the file exists
            if (!File.Exists(normalizedPath))
            {
                Console.WriteLine($"File not found: {normalizedPath}");
                return new FileReview
                {
                    FilePath = normalizedPath,
                    Methods = new List<MethodInfo>()
                };
            }
              // Read file content
            string fileContent = await File.ReadAllTextAsync(normalizedPath);
            
            // Read repository structure to provide context
            string repoStructure = await File.ReadAllTextAsync(_repositoryStructurePath);
            
            // Create prompt for the LLM
            string prompt = CreateFileReviewPrompt(filePath, fileContent, repoStructure);
              // Get the response from the LLM
            var reviewText = await _kernel.InvokePromptAsync(prompt);
            
            // Parse the response into a FileReview object
            var review = ParseFileReview(normalizedPath, reviewText.ToString());
            
            // Save the review to a file
            await SaveReviewToFileAsync(review);
            
            return review;
        }
          /// <summary>
        /// Creates a prompt for identifying methods in a source code file
        /// </summary>
        private string CreateFileReviewPrompt(string filePath, string fileContent, string repositoryStructure)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("You are a code analysis agent that identifies methods in source code files.");
            sb.AppendLine("Your ONLY task is to analyze the provided file and identify all user-defined methods in it.");
            sb.AppendLine("Do NOT provide descriptions, reviews, or any other analysis of the file or methods.");
            sb.AppendLine();
            
            sb.AppendLine("Focus ONLY on user-defined methods. Do not include:");
            sb.AppendLine("1. Built-in or framework methods");
            sb.AppendLine("2. Language keywords or operators");
            sb.AppendLine("3. Property getters/setters unless they contain significant logic");
            sb.AppendLine();
            
            sb.AppendLine("File to review:");
            sb.AppendLine($"Path: {filePath}");
            sb.AppendLine();
            
            sb.AppendLine("File content:");
            sb.AppendLine(fileContent);
            sb.AppendLine();
            
            sb.AppendLine("Repository structure (for context):");
            sb.AppendLine(repositoryStructure);
            sb.AppendLine();
            
            sb.AppendLine("Please provide ONLY a list of methods found in the file in the following JSON format:");
            sb.AppendLine(@"{
  ""methods"": [
    {
      ""name"": ""MethodName"",
      ""source"": ""ClassName""
    }
  ]
}");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Parses the LLM response into a FileReview object
        /// </summary>
        private FileReview ParseFileReview(string filePath, string reviewText)
        {
            // Initialize a default review
            var review = new FileReview
            {
                FilePath = filePath,
                ReviewDate = DateTime.Now
            };
            
            try
            {
                // First, try to extract JSON from the text
                string jsonContent = ExtractJsonFromText(reviewText);
                
                // Parse the JSON
                var extractedReview = JsonSerializer.Deserialize<FileReview>(jsonContent);
                  if (extractedReview != null)
                {
                    // Copy the extracted data
                    review.Methods = extractedReview.Methods ?? new List<MethodInfo>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing file methods: {ex.Message}");
                // Just log the error, but keep an empty methods list
                Console.WriteLine($"Raw response:\n{reviewText}");
            }
            
            return review;
        }
          /// <summary>
        /// Extract JSON content from text that might contain other content around the JSON
        /// </summary>
        private string ExtractJsonFromText(string text)
        {
            // Look for JSON content between { and } brackets
            int startIndex = text.IndexOf('{');
            int endIndex = text.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            // If couldn't find proper JSON markers, return an empty JSON object
            return @"{""methods"": []}";
        }
        
        /// <summary>
        /// Saves the file review to a JSON file
        /// </summary>
        private async Task SaveReviewToFileAsync(FileReview review)
        {            try
            {
                // Create a filename based on the reviewed file
                string fileName = Path.GetFileNameWithoutExtension(review.FilePath);
                string outputPath = Path.Combine(_outputDirectory, $"{fileName}-review.json");
                
                // Serialize and save the review
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(review, options);
                
                await File.WriteAllTextAsync(outputPath, json);
                Console.WriteLine($"File review saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file review: {ex.Message}");
            }
        }
          /// <summary>
        /// Exports the file review to a markdown file for better readability
        /// </summary>
        public async Task<string> ExportReviewToMarkdownAsync(FileReview review)
        {
            StringBuilder sb = new StringBuilder();
            
            // Title and basic info
            sb.AppendLine($"# Methods in {Path.GetFileName(review.FilePath)}");
            sb.AppendLine();
            // Normalize path slashes to use the standard OS-specific path separator
            string normalizedPath = review.FilePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            sb.AppendLine($"**File Path:** `{normalizedPath}`");
            sb.AppendLine();
            
            // Methods
            if (review.Methods.Count > 0)
            {
                sb.AppendLine("## Methods List");
                sb.AppendLine();
                sb.AppendLine("| Method | Source |");
                sb.AppendLine("|--------|--------|");
                
                foreach (var method in review.Methods)
                {
                    sb.AppendLine($"| `{method.Name}` | `{method.Source}` |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("## Methods");
                sb.AppendLine();
                sb.AppendLine("No user-defined methods were identified in this file.");
                sb.AppendLine();
            }
              // Save the markdown file
            string fileName = Path.GetFileNameWithoutExtension(review.FilePath);
            string outputPath = Path.Combine(_outputDirectory, $"{fileName}-review.md");
            
            await File.WriteAllTextAsync(outputPath, sb.ToString());
            
            return outputPath;
        }
    }
}
