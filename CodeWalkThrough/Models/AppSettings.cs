namespace CodeWalkThrough.Models
{
    /// <summary>
    /// Configuration settings for the application
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// SemanticKernel configuration
        /// </summary>
        public SemanticKernelSettings SemanticKernel { get; set; } = new();

        /// <summary>
        /// CodeReviewPlanner configuration
        /// </summary>
        public CodeReviewPlannerSettings CodeReviewPlanner { get; set; } = new();
    }

    /// <summary>
    /// Configuration settings for SemanticKernel
    /// </summary>
    public class SemanticKernelSettings
    {
        /// <summary>
        /// OpenAI configuration
        /// </summary>
        public OpenAISettings OpenAI { get; set; } = new();
    }

    /// <summary>
    /// Configuration settings for OpenAI
    /// </summary>
    public class OpenAISettings
    {
        /// <summary>
        /// OpenAI API key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// OpenAI API endpoint
        /// </summary>
        public string Endpoint { get; set; } = "https://api.openai.com/v1";
        
        /// <summary>
        /// OpenAI model ID
        /// </summary>
        public string ModelId { get; set; } = "gpt-4o";
    }
    
    /// <summary>
    /// Configuration settings for the CodeReviewPlanner
    /// </summary>
    public class CodeReviewPlannerSettings
    {
        /// <summary>
        /// Directory where code review plans are stored
        /// </summary>
        public string OutputDirectory { get; set; } = "code-review-plans";
    }
}
