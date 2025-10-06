namespace ABCRetailCloudSolution.Models
{
    public class FunctionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}