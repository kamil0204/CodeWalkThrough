using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace CodeWalkThrough.Models
{
    /// <summary>
    /// Represents a code review plan with focus areas and priorities
    /// </summary>
    public class CodeReviewPlan
    {
        /// <summary>
        /// Unique identifier for the code review plan
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Title of the code review plan
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Description of the code review plan
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;        /// <summary>
        /// List of components that should be reviewed (could be controllers, services, interfaces, etc.)
        /// </summary>
        [JsonPropertyName("components")]
        public List<ComponentReviewItem> Components { get; set; } = new List<ComponentReviewItem>();
        
        /// <summary>
        /// Maintains backward compatibility with "controllers" field in JSON
        /// </summary>
        [JsonPropertyName("controllers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<ComponentReviewItem>? Controllers 
        { 
            get => Components; 
            set
            {
                if (value != null)
                {
                    Components = value;
                }
            }
        }        /// <summary>
        /// Tech stack identified in the project
        /// </summary>
        [JsonPropertyName("techStack")]
        public List<string> TechStack { get; set; } = new List<string>();
        
        /// <summary>
        /// Categories of files for review
        /// </summary>
        [JsonPropertyName("categories")]
        public List<CategoryReviewItem> Categories { get; set; } = new List<CategoryReviewItem>();
    }    /// <summary>
    /// Represents a code component to be reviewed
    /// </summary>
    public class ComponentReviewItem
    {
        /// <summary>
        /// Name of the component
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the component file
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Priority for reviewing this component (1-5, where 1 is highest)
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 3;
        
        /// <summary>
        /// Areas of focus for this component review
        /// </summary>
        [JsonPropertyName("focusAreas")]
        public List<string> FocusAreas { get; set; } = new List<string>();

        /// <summary>
        /// Rationale for including this component in the review
        /// </summary>
        [JsonPropertyName("rationale")]
        public string Rationale { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of component (e.g., Controller, Service, Repository, etc.)
        /// </summary>
        [JsonPropertyName("componentType")]
        public string ComponentType { get; set; } = string.Empty;
        
        /// <summary>
        /// Estimated complexity of the component (1-5, where 5 is most complex)
        /// </summary>
        [JsonPropertyName("complexity")]
        public int Complexity { get; set; } = 3;
        
        /// <summary>
        /// Related components that may be affected by changes to this component
        /// </summary>
        [JsonPropertyName("relatedComponents")]
        public List<string> RelatedComponents { get; set; } = new List<string>();
        
        /// <summary>
        /// Key technologies or patterns used in this component
        /// </summary>
        [JsonPropertyName("technologies")]
        public List<string> Technologies { get; set; } = new List<string>();
    }    /// <summary>
    /// Represents a category of files in the code review plan
    /// </summary>
    public class CategoryReviewItem
    {
        /// <summary>
        /// Name of the category
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Priority for reviewing this category (1-3, where 1 is highest)
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Description of what this category represents
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Files to review in this category
        /// </summary>
        [JsonPropertyName("files")]
        public List<FileReviewItem> Files { get; set; } = new List<FileReviewItem>();
    }

    /// <summary>
    /// Represents a file to be reviewed within a category
    /// </summary>
    public class FileReviewItem
    {
        /// <summary>
        /// Path to the file
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Reason why this file is important to review
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
