using System.Text;
using System.Text.Json;

namespace ABCRetailCloudSolution.Services
{
    public class FunctionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _functionBaseUrl;
        private readonly string _functionKey;
        private readonly ILogger<FunctionService> _logger;

        public FunctionService(IConfiguration configuration, ILogger<FunctionService> logger)
        {
            _httpClient = new HttpClient();
            _functionBaseUrl = configuration["AzureFunctions:BaseUrl"];
            _functionKey = configuration["AzureFunctions:Key"];
            _logger = logger;

            // Remove any trailing slashes from base URL
            _functionBaseUrl = _functionBaseUrl?.TrimEnd('/');

            _logger.LogInformation($"FunctionService initialized. BaseUrl: {_functionBaseUrl}, Key set: {!string.IsNullOrEmpty(_functionKey)}");
        }

        public async Task<string> AddCustomerAsync(string name, string email, string phone)
        {
            try
            {
                _logger.LogInformation($"Calling AddCustomer function with: {name}, {email}, {phone}");

                // Validate configuration
                if (string.IsNullOrEmpty(_functionBaseUrl) || string.IsNullOrEmpty(_functionKey))
                {
                    var error = "Function configuration is missing. Check appsettings.json";
                    _logger.LogError(error);
                    return $"{{\"Success\": false, \"Message\": \"{error}\"}}";
                }

                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(name), "name");
                formData.Add(new StringContent(email), "email");
                formData.Add(new StringContent(phone), "phone");

                var url = $"{_functionBaseUrl}/api/AddCustomer?code={_functionKey}";
                _logger.LogInformation($"Calling URL: {url}");

                var response = await _httpClient.PostAsync(url, formData);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"AddCustomer response: {response.StatusCode} - {result}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AddCustomer function");
                return $"{{\"Success\": false, \"Message\": \"Error calling function: {ex.Message}\"}}";
            }
        }

        public async Task<string> UploadImageAsync(IFormFile imageFile)
        {
            try
            {
                _logger.LogInformation($"Calling UploadImage function with file: {imageFile?.FileName}, Size: {imageFile?.Length}");

                // Validate configuration
                if (string.IsNullOrEmpty(_functionBaseUrl) || string.IsNullOrEmpty(_functionKey))
                {
                    var error = "Function configuration is missing. Check appsettings.json";
                    _logger.LogError(error);
                    return $"{{\"Success\": false, \"Message\": \"{error}\"}}";
                }

                using var content = new MultipartFormDataContent();
                using var fileStream = imageFile.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(imageFile.ContentType);

                content.Add(fileContent, "imageFile", imageFile.FileName);

                var url = $"{_functionBaseUrl}/api/UploadImage?code={_functionKey}";
                _logger.LogInformation($"Calling URL: {url}");

                var response = await _httpClient.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"UploadImage response: {response.StatusCode} - {result}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UploadImage function");
                return $"{{\"Success\": false, \"Message\": \"Error uploading image: {ex.Message}\"}}";
            }
        }

        public async Task<string> SendQueueMessageAsync(string message)
        {
            try
            {
                _logger.LogInformation($"Calling SendQueueMessage function with: {message}");

                // Validate configuration
                if (string.IsNullOrEmpty(_functionBaseUrl) || string.IsNullOrEmpty(_functionKey))
                {
                    var error = "Function configuration is missing. Check appsettings.json";
                    _logger.LogError(error);
                    return $"{{\"Success\": false, \"Message\": \"{error}\"}}";
                }

                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(message), "message");

                var url = $"{_functionBaseUrl}/api/SendQueueMessage?code={_functionKey}";
                _logger.LogInformation($"Calling URL: {url}");

                var response = await _httpClient.PostAsync(url, formData);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"SendQueueMessage response: {response.StatusCode} - {result}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SendQueueMessage function");
                return $"{{\"Success\": false, \"Message\": \"Error sending message: {ex.Message}\"}}";
            }
        }

        public async Task<string> UploadContractAsync(IFormFile contractFile)
        {
            try
            {
                _logger.LogInformation($"Calling UploadContract function with file: {contractFile?.FileName}, Size: {contractFile?.Length}");

                // Validate configuration
                if (string.IsNullOrEmpty(_functionBaseUrl) || string.IsNullOrEmpty(_functionKey))
                {
                    var error = "Function configuration is missing. Check appsettings.json";
                    _logger.LogError(error);
                    return $"{{\"Success\": false, \"Message\": \"{error}\"}}";
                }

                using var content = new MultipartFormDataContent();
                using var fileStream = contractFile.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contractFile.ContentType);

                content.Add(fileContent, "contractFile", contractFile.FileName);

                var url = $"{_functionBaseUrl}/api/UploadContract?code={_functionKey}";
                _logger.LogInformation($"Calling URL: {url}");

                var response = await _httpClient.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"UploadContract response: {response.StatusCode} - {result}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UploadContract function");
                return $"{{\"Success\": false, \"Message\": \"Error uploading contract: {ex.Message}\"}}";
            }
        }

        // Test method to verify function connectivity
        public async Task<string> TestFunctionConnection()
        {
            try
            {
                if (string.IsNullOrEmpty(_functionBaseUrl) || string.IsNullOrEmpty(_functionKey))
                {
                    return "Configuration missing: BaseUrl or Key is empty";
                }

                var url = $"{_functionBaseUrl}/api/AddCustomer?code={_functionKey}";
                _logger.LogInformation($"Testing connection to: {url}");

                var response = await _httpClient.GetAsync(url);
                return $"Status: {response.StatusCode}, URL: {url}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}, BaseUrl: {_functionBaseUrl}, Key: {_functionKey}";
            }
        }
    }
}