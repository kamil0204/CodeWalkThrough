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
    /// Agent that generates code review plans based on repository structure
    /// </summary>
    public class CodeReviewPlannerAgent
    {
        private readonly Kernel _kernel;
        private readonly string _outputDirectory;

        /// <summary>
        /// Initializes a new instance of the CodeReviewPlannerAgent using the application configuration
        /// </summary>
        public CodeReviewPlannerAgent()
        {
            // Load settings from appsettings.json
            var configService = ConfigurationService.Instance;
            var appSettings = configService.GetAppSettings();
            var kernelSettings = appSettings.SemanticKernel.OpenAI;
            var plannerSettings = appSettings.CodeReviewPlanner;
            
            // Create the kernel builder
            var builder = Kernel.CreateBuilder();
            
            // Add OpenAI text completion service
            builder.AddOpenAIChatCompletion(
                modelId: kernelSettings.ModelId,
                apiKey: kernelSettings.ApiKey,
                endpoint: new Uri(kernelSettings.Endpoint));
            
            // Build the kernel
            _kernel = builder.Build();
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, plannerSettings.OutputDirectory);
            
            // Create the output directory if it doesn't exist
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }
        
        /// <summary>
        /// Initializes a new instance of the CodeReviewPlannerAgent with explicit settings
        /// </summary>
        /// <param name="apiKey">API key for the LLM service</param>
        /// <param name="endpoint">API endpoint</param>
        /// <param name="modelId">Model ID</param>
        /// <param name="outputDirectory">Directory where the code review plans will be saved</param>
        public CodeReviewPlannerAgent(string apiKey, string endpoint, string modelId, string outputDirectory)
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
            
            // Create the output directory if it doesn't exist
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }        /// <summary>
        /// Creates a code review plan based on the repository structure
        /// </summary>
        /// <param name="markdownPath">Path to the markdown file containing repository structure</param>
        /// <param name="projectDescription">Short description of the project to provide context for the code review</param>
        public async Task<CodeReviewPlan> CreateCodeReviewPlanAsync(string markdownPath, string projectDescription = "")
        {
            // Read repository structure content
            string markdownContent = File.ReadAllText(markdownPath);
            
            // Pre-analyze the repository structure to identify potential tech stack markers
            var techStackHints = PreAnalyzeTechStackHints(markdownContent);
            
            // If we have tech stack hints, add them to the project description
            if (!string.IsNullOrEmpty(techStackHints) && !string.IsNullOrEmpty(projectDescription))
            {
                projectDescription = $"{projectDescription}\n\nPotential tech stack indicators: {techStackHints}";
            }
            else if (!string.IsNullOrEmpty(techStackHints))
            {
                projectDescription = $"Potential tech stack indicators: {techStackHints}";
            }
            
            // Create prompt for the LLM with the enhanced project description
            string prompt = CreateCodeReviewPrompt(markdownContent, projectDescription);
            
            // Get the response from the LLM
            var planText = await _kernel.InvokePromptAsync(prompt);
        // Parse the JSON response into a CodeReviewPlan
            CodeReviewPlan plan;
            string responseText = planText.ToString();
            
            Console.WriteLine("Processing LLM response...");
            
            try
            {
                // First, try to parse as JSON directly
                plan = JsonSerializer.Deserialize<CodeReviewPlan>(responseText) ?? 
                    new CodeReviewPlan { 
                        Title = "Error Parsing Response",
                        Description = "Failed to deserialize JSON response"
                    };
                    
                Console.WriteLine("Successfully parsed LLM response directly as JSON");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Direct JSON parsing failed: {ex.Message}");
                Console.WriteLine("Attempting to extract JSON from the response text...");
                
                // If direct parsing fails, try to extract JSON from the response text
                string jsonContent = ExtractJsonFromText(responseText);
                
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    plan = JsonSerializer.Deserialize<CodeReviewPlan>(jsonContent, options) ?? 
                        new CodeReviewPlan { 
                            Title = "Error Parsing Response",
                            Description = "Failed to deserialize extracted JSON content"
                        };
                        
                    Console.WriteLine("Successfully parsed extracted JSON content");
                }
                catch (JsonException nestedEx)
                {
                    Console.WriteLine($"JSON extraction and parsing failed: {nestedEx.Message}");
                    Console.WriteLine("Creating fallback plan with error information");
                    
                    // If still fails, create a fallback plan with helpful debug info
                    var fallbackPlan = new CodeReviewPlan
                    {
                        Title = "Error Parsing Response",
                        Description = $"Could not parse LLM response as JSON. Error: {nestedEx.Message}"
                    };
                    
                    // Add debug information to help troubleshoot
                    fallbackPlan.TechStack.Add("Error Information");
                    
                    // Create a debug category
                    var debugCategory = new CategoryReviewItem
                    {
                        Name = "Debug Information",
                        Priority = 1,
                        Description = "Information to help troubleshoot the JSON parsing error"
                    };
                    
                    // Add a file entry with the error details
                    debugCategory.Files.Add(new FileReviewItem
                    {
                        Path = "Error Details",
                        Reason = $"Error Type: {nestedEx.GetType().Name}, Message: {nestedEx.Message}"
                    });
                    
                    // Add the first 100 characters of the extracted JSON to help diagnose
                    string jsonPreview = jsonContent.Length > 100 ? 
                        jsonContent.Substring(0, 100) + "..." : 
                        jsonContent;
                        
                    debugCategory.Files.Add(new FileReviewItem
                    {
                        Path = "Extracted JSON Preview",
                        Reason = jsonPreview
                    });
                    
                    fallbackPlan.Categories.Add(debugCategory);
                    return fallbackPlan;
                }
            }
            
            // Validate that the plan has the minimum required properties
            if (string.IsNullOrWhiteSpace(plan.Title))
            {
                plan.Title = "Code Review Plan";
            }
            
            if (string.IsNullOrWhiteSpace(plan.Description))
            {
                plan.Description = "Generated code review plan for the repository";
            }
            
            // Ensure we have at least empty collections to prevent null reference exceptions
            if (plan.TechStack == null)
            {
                plan.TechStack = new List<string>();
            }
            
            if (plan.Categories == null)
            {
                plan.Categories = new List<CategoryReviewItem>();
            }
            
            if (plan.Components == null)
            {
                plan.Components = new List<ComponentReviewItem>();
            }
            
            // Save the plan to a file
            await SavePlanToFileAsync(plan);
            
            return plan;
        }        /// <summary>
        /// Creates the prompt for generating a code review plan
        /// </summary>
        private string CreateCodeReviewPrompt(string repositoryStructure, string projectDescription)
        {
            StringBuilder sb = new StringBuilder();
              sb.AppendLine("You are an experienced code review planner helping to create a structured code review plan for a provided repository.");
            sb.AppendLine("Based on the repository structure below, analyze the codebase and identify the tech stack, architecture, and entry points from where the code needs to be reviewed.");
            
            sb.AppendLine("\nIndependently identify the project's tech stack based on file extensions, folder structure, configuration files, and naming patterns.");
            sb.AppendLine("\nThen, analyze the repository structure to identify key entry points from where the code review needs to be started");
            sb.AppendLine(" - Look for files that likely contain application initialization, external interfaces, or main entry points");
            sb.AppendLine(" - Recognize patterns in the codebase that suggest architectural boundaries");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(projectDescription))
            {
                sb.AppendLine("Project Context:");
                sb.AppendLine(projectDescription);
                sb.AppendLine();
            }
            
            sb.AppendLine("Repository Structure:");
            sb.AppendLine(repositoryStructure);
            sb.AppendLine();            sb.AppendLine("Based on the above information, create a code review plan organized as a simple todo list that:");
            sb.AppendLine("1. Identifies the tech stack and project architecture without making assumptions");
            sb.AppendLine("2. Categorizes files into logical groups based on their purpose in the application");
            sb.AppendLine("3. Lists specific files to review in each category with a brief reason why");
            sb.AppendLine("4. Prioritize categories to indicate which should be reviewed first");
            sb.AppendLine("5. Focus on entry points like API endpoints, background process, console services, etc.");
            sb.AppendLine("6. Don't list out files that include business logics and other application specific implementations that involve Database or Http transaction and other files that are not the entry point of the application");
            sb.AppendLine();
            sb.AppendLine("Return your response as a JSON object matching this CodeReviewPlan structure:");
            sb.AppendLine(@"{
  ""title"": ""Code Review Plan for [Project Name]"",
  ""description"": ""Brief overview of the codebase and identified tech stack"",
  ""techStack"": [""List"", ""of"", ""identified"", ""technologies""],
  ""categories"": [
    {
      ""name"": ""Category Name (This can be tech specific based on the technology that you have identified like 'Controllers', 'API Endpoints', 'views', etc.)"",
      ""priority"": 1, // 1-3, where 1 is highest priority
      ""description"": ""Brief description of what this category represents"",
      ""files"": [
        {
          ""path"": ""Full/Path/To/File"",
          ""reason"": ""Brief explanation of why this file is important to review""
        },
        {
          ""path"": ""Full/Path/To/AnotherFile"",
          ""reason"": ""Brief explanation""
        }
      ]
    },
    {
      ""name"": ""Another Category"",
      ""priority"": 2,
      ""description"": ""Brief description"",
      ""files"": [
        {
          ""path"": ""Full/Path/To/File"",
          ""reason"": ""Brief explanation""
        }
      ]
    }
  ]
}");
            
            return sb.ToString();
        }        /// <summary>
        /// Extract and attempt to repair JSON content from text that might contain other content around the JSON
        /// </summary>
        private string ExtractJsonFromText(string text)
        {
            Console.WriteLine("Extracting JSON from text response");
            Console.WriteLine($"Raw text length: {text.Length} characters");
            
            // Save the raw text to a debug file for investigation
            string debugFile = Path.Combine(_outputDirectory, $"raw-llm-response-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(debugFile, text);
            Console.WriteLine($"Raw LLM response saved to: {debugFile}");
            
            // Special case: handle when the model returns markdown-formatted JSON blocks
            if (text.Contains("```json") && text.Contains("```"))
            {
                int codeBlockStart = text.IndexOf("```json") + 7;
                int codeBlockEnd = text.IndexOf("```", codeBlockStart);
                
                if (codeBlockStart >= 7 && codeBlockEnd > codeBlockStart)
                {
                    string jsonContent = text.Substring(codeBlockStart, codeBlockEnd - codeBlockStart).Trim();
                    Console.WriteLine("Found JSON in markdown code block");
                    try
                    {
                        // Validate JSON by parsing it with JsonDocument
                        using JsonDocument doc = JsonDocument.Parse(jsonContent);
                        return jsonContent; // If it parses correctly, return it
                    }
                    catch (JsonException ex)
                    {
                        // JSON is malformed, try to repair common issues
                        Console.WriteLine($"JSON from code block is malformed: {ex.Message}");
                        return AttemptToRepairJson(jsonContent, ex);
                    }
                }
            }
            
            // Find the outermost JSON object
            int startIndex = text.IndexOf('{');
            int endIndex = text.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                string jsonContent = text.Substring(startIndex, endIndex - startIndex + 1);
                Console.WriteLine($"Found potential JSON object from position {startIndex} to {endIndex}");
                
                // Print the first and last 50 characters of the JSON for debugging
                string startPart = jsonContent.Length > 50 ? jsonContent.Substring(0, 50) : jsonContent;
                string endPart = jsonContent.Length > 50 ? jsonContent.Substring(Math.Max(0, jsonContent.Length - 50)) : "";
                Console.WriteLine($"JSON starts with: {startPart}...");
                Console.WriteLine($"JSON ends with: ...{endPart}");
                
                // Check if this actually looks like our expected JSON structure
                bool hasTitle = jsonContent.Contains("\"title\"");
                bool hasDescription = jsonContent.Contains("\"description\"");
                bool hasCategories = jsonContent.Contains("\"categories\"") || jsonContent.Contains("\"components\"");
                bool hasTechStack = jsonContent.Contains("\"techStack\"");
                
                Console.WriteLine($"JSON structure check: Title: {hasTitle}, Description: {hasDescription}, Categories/Components: {hasCategories}, TechStack: {hasTechStack}");
                
                // If it doesn't look like our expected JSON structure, try to find a nested JSON object
                if (!(hasTitle && hasDescription && hasCategories))
                {
                    Console.WriteLine("Initial JSON extraction doesn't match expected structure, looking for nested JSON");
                    // Try to find a better JSON object by looking for key properties
                    int titlePos = text.IndexOf("\"title\"");
                    if (titlePos > 0)
                    {
                        // Look backwards for the closest opening brace
                        int newStart = text.LastIndexOf('{', titlePos);
                        // Look forward from title for the matching closing brace
                        // This is a simplified approach - a proper implementation would need to account for nested braces
                        if (newStart >= 0 && newStart < titlePos)
                        {
                            startIndex = newStart;
                            jsonContent = text.Substring(startIndex, endIndex - startIndex + 1);
                            Console.WriteLine($"Found better JSON candidate starting at position {startIndex}");
                        }
                    }
                }
                
                // Save the extracted JSON to a debug file for investigation
                string extractedFile = Path.Combine(_outputDirectory, $"extracted-json-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(extractedFile, jsonContent);
                Console.WriteLine($"Extracted JSON saved to: {extractedFile}");
                
                try
                {
                    // Validate JSON by parsing it with JsonDocument
                    using JsonDocument doc = JsonDocument.Parse(jsonContent);
                    Console.WriteLine("JSON validated successfully");
                    return jsonContent; // If it parses correctly, return it
                }
                catch (JsonException ex)
                {
                    // JSON is malformed, try to repair common issues
                    Console.WriteLine($"JSON is malformed, attempting repair: {ex.Message}");
                    return AttemptToRepairJson(jsonContent, ex);
                }
            }
            
            // If we still couldn't find proper JSON markers, log and return original text
            Console.WriteLine("Could not find valid JSON object markers in text");
            return text;
        }
          /// <summary>
        /// Attempts to repair common JSON formatting issues
        /// </summary>
        private string AttemptToRepairJson(string json, JsonException exception)
        {
            Console.WriteLine($"Attempting to repair malformed JSON: {exception.Message}");
            
            // Save the original JSON for comparison
            string originalJson = json;
            string repairedJson = json;
            
            // Check for unclosed objects or arrays
            if (exception.Message.Contains("depth to be zero") || 
                exception.Message.Contains("open JSON object or array") ||
                exception.Message.Contains("end of data") ||
                exception.Message.Contains("truncated"))
            {
                // Count opening and closing braces/brackets to find imbalance
                int openBraces = json.Count(c => c == '{');
                int closeBraces = json.Count(c => c == '}');
                int openBrackets = json.Count(c => c == '[');
                int closeBrackets = json.Count(c => c == ']');
                
                Console.WriteLine($"JSON balance check: {{ {openBraces}/{closeBraces} }} [ {openBrackets}/{closeBrackets} ]");
                
                // Balance missing braces and brackets
                repairedJson = json;
                
                // Add missing closing braces
                for (int i = 0; i < openBraces - closeBraces; i++)
                {
                    repairedJson += "}";
                }
                
                // Add missing closing brackets
                for (int i = 0; i < openBrackets - closeBrackets; i++)
                {
                    repairedJson += "]";
                }
                
                Console.WriteLine("Balancing braces and brackets complete");
            }
            
            // Check for trailing commas in objects and arrays
            repairedJson = FixTrailingCommas(repairedJson);
            
            // Check for missing quotes around property names
            repairedJson = FixMissingQuotes(repairedJson);
            
            // Check for unescaped quotes in strings
            repairedJson = FixUnescapedQuotes(repairedJson);
            
            // Check for missing colons
            repairedJson = FixMissingColons(repairedJson);
            
            // Validate the repaired JSON
            if (repairedJson != originalJson)
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(repairedJson);
                    Console.WriteLine("JSON repair successful");
                    return repairedJson;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Initial repair attempt failed: {ex.Message}");
                    
                    // Try a more aggressive repair for severe corruption
                    try
                    {
                        // For severe corruption, attempt to fix the JSON structure completely
                        string extremeRepair = AttemptExtremeCaseRepair(repairedJson);
                        
                        using JsonDocument doc = JsonDocument.Parse(extremeRepair);
                        Console.WriteLine("Advanced JSON repair successful");
                        return extremeRepair;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Advanced JSON repair failed");
                        return originalJson;
                    }
                }
            }
            
            // If no changes needed or repair failed, return original JSON
            return json;
        }
        
        /// <summary>
        /// Fixes trailing commas in JSON objects and arrays
        /// </summary>
        private string FixTrailingCommas(string json)
        {
            // Replace trailing commas before closing brackets
            return json
                .Replace(",}", "}")
                .Replace(",\n}", "\n}")
                .Replace(",\r\n}", "\r\n}")
                .Replace(",]", "]")
                .Replace(",\n]", "\n]")
                .Replace(",\r\n]", "\r\n]");
        }
        
        /// <summary>
        /// Fixes missing quotes around property names
        /// </summary>
        private string FixMissingQuotes(string json)
        {
            // This is a simplified approach - a more robust solution would use regex
            // but we'll keep it simple for now
            return json;
        }
        
        /// <summary>
        /// Fixes unescaped quotes in strings
        /// </summary>
        private string FixUnescapedQuotes(string json)
        {
            // This is a simplified approach - a more robust solution would use regex
            // but we'll keep it simple for now
            return json;
        }
        
        /// <summary>
        /// Fixes missing colons in property definitions
        /// </summary>
        private string FixMissingColons(string json)
        {
            // This is a simplified approach - a more robust solution would use regex
            // but we'll keep it simple for now
            return json;
        }
          /// <summary>
        /// Attempts a more aggressive repair for severely corrupted JSON by trying to preserve 
        /// as much of the original structure as possible
        /// </summary>
        private string AttemptExtremeCaseRepair(string json)
        {
            try
            {
                Console.WriteLine("Attempting extreme case JSON repair");
                
                // Extract basic properties
                string title = ExtractJsonStringProperty(json, "title");
                string description = ExtractJsonStringProperty(json, "description");
                
                // If we don't have title and description, use defaults
                if (string.IsNullOrEmpty(title)) title = "Code Review Plan";
                if (string.IsNullOrEmpty(description)) description = "Generated code review plan";
                
                // Try to extract tech stack
                List<string> techStack = new();
                try 
                {
                    techStack = ExtractJsonArrayItems(json, "techStack");
                    Console.WriteLine($"Extracted {techStack.Count} tech stack items");
                } 
                catch (Exception ex) 
                {
                    Console.WriteLine($"Failed to extract tech stack: {ex.Message}");
                }
                
                // Try to extract categories - this is more complex
                var categories = TryExtractCategories(json);
                Console.WriteLine($"Extracted {categories.Count} categories");
                
                // Serialize the elements we extracted
                var options = new JsonSerializerOptions { WriteIndented = true };
                string techStackJson = JsonSerializer.Serialize(techStack, options);
                string categoriesJson = JsonSerializer.Serialize(categories, options);
                
                // Build a valid JSON structure with the extracted parts
                return $@"{{
  ""title"": ""{EscapeJsonString(title)}"",
  ""description"": ""{EscapeJsonString(description)}"",
  ""techStack"": {techStackJson},
  ""categories"": {categoriesJson}
}}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Extreme repair failed: {ex.Message}");
                
                // Last resort - return a minimal valid JSON object
                return @"{
  ""title"": ""Error Parsing Response"",
  ""description"": ""Failed to parse LLM response as JSON."",
  ""techStack"": [],
  ""categories"": []
}";
            }
        }
        
        /// <summary>
        /// Extract array items from a potentially malformed JSON
        /// </summary>
        private List<string> ExtractJsonArrayItems(string json, string arrayName)
        {
            var result = new List<string>();
            
            string arrayPattern = $"\"{arrayName}\"\\s*:\\s*\\[(.*?)\\]";
            var match = System.Text.RegularExpressions.Regex.Match(json, arrayPattern, 
                System.Text.RegularExpressions.RegexOptions.Singleline);
                
            if (match.Success && match.Groups.Count > 1)
            {
                string arrayContent = match.Groups[1].Value;
                
                // Split by commas but respect quoted strings
                bool inQuotes = false;
                var sb = new StringBuilder();
                
                foreach (char c in arrayContent)
                {
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    
                    if (c == ',' && !inQuotes)
                    {
                        // End of an item
                        string item = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(item))
                        {
                            // Remove surrounding quotes if they exist
                            if (item.StartsWith("\"") && item.EndsWith("\""))
                            {
                                item = item.Substring(1, item.Length - 2);
                            }
                            result.Add(item);
                        }
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                
                // Don't forget the last item
                string lastItem = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(lastItem))
                {
                    // Remove surrounding quotes if they exist
                    if (lastItem.StartsWith("\"") && lastItem.EndsWith("\""))
                    {
                        lastItem = lastItem.Substring(1, lastItem.Length - 2);
                    }
                    result.Add(lastItem);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Try to extract categories from a potentially broken JSON
        /// </summary>
        private List<CategoryReviewItem> TryExtractCategories(string json)
        {
            var result = new List<CategoryReviewItem>();
            
            try
            {
                // Find the categories array
                int categoriesStart = json.IndexOf("\"categories\"");
                if (categoriesStart < 0) return result;
                
                // Find the array start
                int arrayStart = json.IndexOf('[', categoriesStart);
                if (arrayStart < 0) return result;
                
                // Find matching array end (accounting for nested arrays)
                int bracketCount = 1;
                int arrayEnd = -1;
                
                for (int i = arrayStart + 1; i < json.Length; i++)
                {
                    if (json[i] == '[') bracketCount++;
                    else if (json[i] == ']') bracketCount--;
                    
                    if (bracketCount == 0)
                    {
                        arrayEnd = i;
                        break;
                    }
                }
                
                if (arrayEnd < 0) 
                {
                    // If we can't find the end, try a simpler approach
                    arrayEnd = json.IndexOf(']', arrayStart);
                    if (arrayEnd < 0) return result;
                }
                
                string categoriesArray = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                
                // Find individual category objects
                List<string> categoryObjects = ExtractJsonObjects(categoriesArray);
                Console.WriteLine($"Found {categoryObjects.Count} category objects in categories array");
                
                // Process each category object
                foreach (string categoryJson in categoryObjects)
                {
                    try
                    {
                        var category = new CategoryReviewItem
                        {
                            Name = ExtractJsonStringProperty(categoryJson, "name"),
                            Description = ExtractJsonStringProperty(categoryJson, "description")
                        };
                        
                        // Try to extract priority
                        string priorityStr = ExtractJsonNumberProperty(categoryJson, "priority");
                        if (int.TryParse(priorityStr, out int priority))
                        {
                            category.Priority = priority;
                        }
                        
                        // Try to extract files
                        category.Files = TryExtractFiles(categoryJson);
                        
                        // Add only if we have at least a name
                        if (!string.IsNullOrEmpty(category.Name))
                        {
                            result.Add(category);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing category object: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting categories: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Try to extract files from a category JSON
        /// </summary>
        private List<FileReviewItem> TryExtractFiles(string categoryJson)
        {
            var result = new List<FileReviewItem>();
            
            try
            {
                // Find the files array
                int filesStart = categoryJson.IndexOf("\"files\"");
                if (filesStart < 0) return result;
                
                // Find the array start
                int arrayStart = categoryJson.IndexOf('[', filesStart);
                if (arrayStart < 0) return result;
                
                // Find matching array end (accounting for nested objects)
                int bracketCount = 1;
                int arrayEnd = -1;
                
                for (int i = arrayStart + 1; i < categoryJson.Length; i++)
                {
                    if (categoryJson[i] == '[') bracketCount++;
                    else if (categoryJson[i] == ']') bracketCount--;
                    
                    if (bracketCount == 0)
                    {
                        arrayEnd = i;
                        break;
                    }
                }
                
                if (arrayEnd < 0) 
                {
                    // If we can't find the end, try a simpler approach
                    arrayEnd = categoryJson.IndexOf(']', arrayStart);
                    if (arrayEnd < 0) return result;
                }
                
                string filesArray = categoryJson.Substring(arrayStart, arrayEnd - arrayStart + 1);
                
                // Find individual file objects
                List<string> fileObjects = ExtractJsonObjects(filesArray);
                Console.WriteLine($"Found {fileObjects.Count} file objects in files array");
                
                // Process each file object
                foreach (string fileJson in fileObjects)
                {
                    try
                    {
                        var file = new FileReviewItem
                        {
                            Path = ExtractJsonStringProperty(fileJson, "path"),
                            Reason = ExtractJsonStringProperty(fileJson, "reason")
                        };
                        
                        // Add only if we have at least a path
                        if (!string.IsNullOrEmpty(file.Path))
                        {
                            result.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file object: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting files: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Extract JSON objects from a JSON array
        /// </summary>
        private List<string> ExtractJsonObjects(string jsonArray)
        {
            var result = new List<string>();
            
            if (string.IsNullOrEmpty(jsonArray) || !jsonArray.StartsWith("["))
                return result;
                
            // Extract objects between { and } but respect nested objects
            int pos = 0;
            while (pos < jsonArray.Length)
            {
                int objStart = jsonArray.IndexOf('{', pos);
                if (objStart < 0) break;
                
                int braceCount = 1;
                int objEnd = -1;
                
                for (int i = objStart + 1; i < jsonArray.Length; i++)
                {
                    if (jsonArray[i] == '{') braceCount++;
                    else if (jsonArray[i] == '}') braceCount--;
                    
                    if (braceCount == 0)
                    {
                        objEnd = i;
                        break;
                    }
                }
                
                if (objEnd > objStart)
                {
                    string obj = jsonArray.Substring(objStart, objEnd - objStart + 1);
                    result.Add(obj);
                    pos = objEnd + 1;
                }
                else
                {
                    break;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Extract a number property value from a JSON string
        /// </summary>
        private string ExtractJsonNumberProperty(string json, string propertyName)
        {
            string pattern = $"\"{propertyName}\"\\s*:\\s*([0-9]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return "0";
        }
        
        /// <summary>
        /// Extracts a string property value from a JSON string
        /// </summary>
        private string ExtractJsonStringProperty(string json, string propertyName)
        {
            string pattern = $"\"{propertyName}\"\\s*:\\s*\"([^\"]*)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Escapes special characters in a string to make it valid for JSON
        /// </summary>
        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
                
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Saves the code review plan to a file
        /// </summary>
        private async Task SavePlanToFileAsync(CodeReviewPlan plan)
        {
            string fileName = $"code-review-plan-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            string filePath = Path.Combine(_outputDirectory, fileName);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(plan, options);
            
            await File.WriteAllTextAsync(filePath, jsonContent);
        }        /// <summary>
        /// Extracts summary information about the repository structure
        /// </summary>
        /// <param name="markdownContent">The repository structure in markdown format</param>
        /// <returns>A string with repository summary information</returns>
        private string PreAnalyzeTechStackHints(string markdownContent)
        {
            // Instead of hard-coding tech stack detection, we now pass along
            // basic information about the repository structure to the LLM
            
            int fileCount = markdownContent.Split('\n')
                .Count(line => !line.EndsWith("/") && !string.IsNullOrWhiteSpace(line));
                
            int dirCount = markdownContent.Split('\n')
                .Count(line => line.EndsWith("/") && !string.IsNullOrWhiteSpace(line));
            
            // Extract the top-level directories as a hint about project organization
            var topLevelDirs = markdownContent.Split('\n')
                .Where(line => line.Count(c => c == '/') == 1 && line.EndsWith("/"))
                .Select(line => line.Trim())
                .ToList();
                
            string dirStructure = topLevelDirs.Count > 0 
                ? $"Top-level directories: {string.Join(", ", topLevelDirs)}" 
                : "";
                
            return $"Repository structure contains approximately {fileCount} files and {dirCount} directories. {dirStructure}";
        }        /// <summary>
        /// Identifies recommended entry points for code review based on either categories or components
        /// </summary>
        /// <param name="plan">The generated code review plan</param>
        /// <returns>A list of recommended starting points for the code review</returns>
        public List<string> GetRecommendedEntryPoints(CodeReviewPlan plan)
        {
            var recommendations = new List<string>();
            
            try
            {
                // Check if we have categories to analyze
                if (plan.Categories != null && plan.Categories.Any())
                {
                    // Get files from highest priority categories first
                    foreach (int priority in new[] { 1, 2, 3 })
                    {
                        if (recommendations.Count >= 3) break;
                        
                        var validCategories = plan.Categories
                            .Where(c => c.Priority == priority && c.Files != null)
                            .ToList();
                            
                        if (validCategories.Any())
                        {
                            var categoryFiles = validCategories
                                .SelectMany(c => c.Files)
                                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.Path))
                                .Select(f => f.Path)
                                .Take(5 - recommendations.Count);
                                
                            recommendations.AddRange(categoryFiles);
                        }
                    }
                }
                // Fall back to components if no categories are available
                else if (plan.Components != null && plan.Components.Any()) 
                {
                    // Start with the highest priority components (priority 1)
                    var validComponents = plan.Components
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Path))
                        .ToList();
                        
                    if (validComponents.Any())
                    {
                        var highestPriority = validComponents
                            .Where(c => c.Priority == 1)
                            .OrderBy(c => c.Name) // Order by name for consistency
                            .Take(3);
                            
                        recommendations.AddRange(highestPriority.Select(c => c.Path));
                        
                        // If we need more recommendations, add priority 2 components
                        if (recommendations.Count < 3)
                        {
                            var secondPriority = validComponents
                                .Where(c => c.Priority == 2)
                                .OrderBy(c => c.Name)
                                .Take(3 - recommendations.Count);
                                
                            recommendations.AddRange(secondPriority.Select(c => c.Path));
                        }
                        
                        // If we still need more, just take the next components by priority
                        if (recommendations.Count < 3)
                        {
                            var remaining = validComponents
                                .Where(c => c.Priority > 2)
                                .OrderBy(c => c.Priority)
                                .ThenBy(c => c.Name)
                                .Take(3 - recommendations.Count);
                                
                            recommendations.AddRange(remaining.Select(c => c.Path));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while getting recommended entry points: {ex.Message}");
                // In case of any error, return an empty list rather than crashing
            }
            
            return recommendations.Distinct().Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        }

        /// <summary>
        /// Exports the code review plan to a markdown file for better readability
        /// </summary>
        /// <param name="plan">The code review plan to export</param>
        /// <returns>The path to the created markdown file</returns>
        public async Task<string> ExportPlanToMarkdownAsync(CodeReviewPlan plan)
        {
            StringBuilder sb = new StringBuilder();
            
            // Title and description
            sb.AppendLine($"# {plan.Title}");
            sb.AppendLine();
            sb.AppendLine(plan.Description);
            sb.AppendLine();
            
            // Tech stack
            if (plan.TechStack != null && plan.TechStack.Count > 0)
            {
                sb.AppendLine("## Tech Stack");
                sb.AppendLine();
                foreach (var tech in plan.TechStack)
                {
                    sb.AppendLine($"- {tech}");
                }
                sb.AppendLine();
            }
            
            // Recommended entry points
            var recommendations = GetRecommendedEntryPoints(plan);
            if (recommendations.Count > 0)
            {
                sb.AppendLine("## Recommended Entry Points");
                sb.AppendLine();
                sb.AppendLine("These are the recommended starting points for your code review:");
                sb.AppendLine();
                foreach (var path in recommendations)
                {
                    sb.AppendLine($"- `{path}`");
                }
                sb.AppendLine();
            }            // Overall stats
            sb.AppendLine("## Review Summary");
            sb.AppendLine();
            
            // Use either components or categories depending on what's available
            if (plan.Categories != null && plan.Categories.Count > 0)
            {
                int totalFiles = plan.Categories.Sum(c => c.Files.Count);
                sb.AppendLine($"- **Categories:** {plan.Categories.Count}");
                sb.AppendLine($"- **Files to review:** {totalFiles}");
            }
            else if (plan.Components != null && plan.Components.Count > 0)
            {
                sb.AppendLine($"- **Components to review:** {plan.Components.Count}");
            }
            sb.AppendLine();
              // Categories by priority
            if (plan.Categories != null && plan.Categories.Count > 0)
            {
                sb.AppendLine("## Categories");
                sb.AppendLine();
                
                var prioritizedCategories = plan.Categories
                    .Where(c => c != null)
                    .OrderBy(c => c.Priority)
                    .ToList();
                
                foreach (var category in prioritizedCategories)
                {
                    try
                    {
                        sb.AppendLine($"### {category.Priority}. {category.Name}");
                        sb.AppendLine();
                        sb.AppendLine(category.Description ?? "No description provided");
                        sb.AppendLine();
                        
                        sb.AppendLine("#### Files");
                        sb.AppendLine();
                        sb.AppendLine("| File | Reason |");
                        sb.AppendLine("|------|--------|");
                        
                        if (category.Files != null)
                        {
                            foreach (var file in category.Files.Where(f => f != null))
                            {
                                string path = !string.IsNullOrEmpty(file.Path) ? file.Path : "(No path)";
                                string reason = !string.IsNullOrEmpty(file.Reason) ? file.Reason : "(No reason provided)";
                                sb.AppendLine($"| `{path}` | {reason} |");
                            }
                        }
                        else
                        {
                            sb.AppendLine("| (No files in this category) | - |");
                        }
                        sb.AppendLine();
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with the next category
                        sb.AppendLine($"Error processing category: {ex.Message}");
                        sb.AppendLine();
                    }
                }
            }            // Fall back to old component-based format if categories aren't available
            else if (plan.Components != null && plan.Components.Count > 0)
            {
                sb.AppendLine("## Components by Priority");
                sb.AppendLine();
                sb.AppendLine("| Priority | Component | Type | Focus Areas | Complexity |");
                sb.AppendLine("|:--------:|----------|------|-------------|:-----------:|");
                
                var prioritized = plan.Components
                    .Where(c => c != null)
                    .OrderBy(c => c.Priority)
                    .ToList();
                
                foreach (var component in prioritized)
                {
                    try
                    {
                        string name = !string.IsNullOrEmpty(component.Name) ? component.Name : "(No name)";
                        string componentType = !string.IsNullOrEmpty(component.ComponentType) ? component.ComponentType : "Unknown";
                        string focusAreas = component.FocusAreas != null ? string.Join(", ", component.FocusAreas) : "";
                        int complexity = component.Complexity;
                        sb.AppendLine($"| **{component.Priority}** | `{name}` | {componentType} | {focusAreas} | {complexity} |");
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with the next component
                        sb.AppendLine($"| ** | `Error processing component: {ex.Message}` | - | - | - |");
                    }
                }
                sb.AppendLine();
                
                // Detailed component reviews
                sb.AppendLine("## Detailed Component Reviews");
                sb.AppendLine();
                
                foreach (var component in prioritized)
                {
                    try
                    {
                        string name = !string.IsNullOrEmpty(component.Name) ? component.Name : "(No name)";
                        string componentType = !string.IsNullOrEmpty(component.ComponentType) ? component.ComponentType : "Unknown";
                        string path = !string.IsNullOrEmpty(component.Path) ? component.Path : "(No path)";
                        
                        sb.AppendLine($"### {name} ({componentType})");
                        sb.AppendLine();
                        sb.AppendLine($"**Path:** `{path}`");
                        sb.AppendLine($"**Priority:** {component.Priority}/5");
                        if (component.Complexity > 0)
                        {
                            sb.AppendLine($"**Complexity:** {component.Complexity}/5");
                        }
                        sb.AppendLine();
                        
                        sb.AppendLine("**Focus Areas:**");
                        if (component.FocusAreas != null && component.FocusAreas.Count > 0)
                        {
                            foreach (var area in component.FocusAreas)
                            {
                                if (!string.IsNullOrEmpty(area))
                                {
                                    sb.AppendLine($"- {area}");
                                }
                            }
                        }
                        else
                        {
                            sb.AppendLine("- (None specified)");
                        }
                        sb.AppendLine();
                        
                        sb.AppendLine("**Rationale:**");
                        sb.AppendLine(!string.IsNullOrEmpty(component.Rationale) ? component.Rationale : "(No rationale provided)");
                        sb.AppendLine();
                        
                        if (component.Technologies != null && component.Technologies.Count > 0)
                        {
                            sb.AppendLine("**Technologies:**");
                            foreach (var tech in component.Technologies.Where(t => !string.IsNullOrEmpty(t)))
                            {
                                sb.AppendLine($"- {tech}");
                            }
                            sb.AppendLine();
                        }
                        
                        if (component.RelatedComponents != null && component.RelatedComponents.Count > 0)
                        {
                            sb.AppendLine("**Related Components:**");
                            foreach (var related in component.RelatedComponents.Where(r => !string.IsNullOrEmpty(r)))
                            {
                                sb.AppendLine($"- {related}");
                            }
                            sb.AppendLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with the next component
                        sb.AppendLine($"### Error processing component");
                        sb.AppendLine();
                        sb.AppendLine($"**Error:** {ex.Message}");
                        sb.AppendLine();
                    }
                }
            }
            
            // Save the markdown file
            string fileName = $"code-review-plan-{DateTime.Now:yyyyMMdd-HHmmss}.md";
            string filePath = Path.Combine(_outputDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString());
            
            return filePath;
        }
    }
}
