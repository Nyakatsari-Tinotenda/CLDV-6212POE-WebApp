using ABCRetailCloudSolution.Services;
using ABCRetailCloudSolution.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ABCRetailCloudSolution.Controllers
{
    public class HomeController : Controller
    {
        private readonly AzureStorageService _storageService;
        private readonly FunctionService _functionService;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(AzureStorageService storageService, FunctionService functionService, ILogger<HomeController> logger, IConfiguration configuration)
        {
            _storageService = storageService;
            _functionService = functionService;
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Customer Management
        [HttpGet]
        public IActionResult AddCustomer()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddCustomer(CustomerProfile customer)
        {
            try
            {
                _logger.LogInformation($"Starting AddCustomer for: {customer.Name}");

                // Call Azure Function to add customer
                _logger.LogInformation("Calling FunctionService.AddCustomerAsync...");
                var result = await _functionService.AddCustomerAsync(customer.Name, customer.Email, customer.Phone);
                _logger.LogInformation($"FunctionService returned: {result}");

                // Parse the JSON response
                try
                {
                    var response = JsonSerializer.Deserialize<FunctionResponse>(result);
                    if (response?.Success == true)
                    {
                        TempData["SuccessMessage"] = response.Message;
                        _logger.LogInformation($"Success: {response.Message}");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = response?.Message ?? "Failed to add customer";
                        _logger.LogWarning($"Failed: {response?.Message}");
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning($"JSON parse failed, using raw result: {result}");
                    TempData["SuccessMessage"] = result;
                }

                // Also send a queue message
                _logger.LogInformation("Sending queue message...");
                await _functionService.SendQueueMessageAsync($"New customer registered: {customer.Name}");
                _logger.LogInformation("Queue message sent successfully");

                _logger.LogInformation("AddCustomer completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddCustomer");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Customers");
        }

        public async Task<IActionResult> Customers()
        {
            try
            {
                var customers = await _storageService.GetCustomersAsync();
                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                TempData["ErrorMessage"] = "Error loading customers";
                return View(new List<CustomerProfile>());
            }
        }

        // Image Management
        [HttpGet]
        public IActionResult UploadImage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile imageFile)
        {
            try
            {
                _logger.LogInformation($"Starting UploadImage for: {imageFile?.FileName}");

                if (imageFile != null && imageFile.Length > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file type. Please upload JPG, PNG, GIF, BMP, or WebP images.";
                        return RedirectToAction("UploadImage");
                    }

                    // Validate file size (10MB limit)
                    if (imageFile.Length > 10 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must be less than 10MB.";
                        return RedirectToAction("UploadImage");
                    }

                    // **DIRECT UPLOAD TO AZURE STORAGE**
                    _logger.LogInformation("Uploading directly to Azure Storage...");
                    var imageUrl = await _storageService.UploadImageAsync(imageFile);
                    _logger.LogInformation($"Image uploaded successfully: {imageUrl}");

                    TempData["SuccessMessage"] = $"Image '{imageFile.FileName}' uploaded successfully to Azure Storage! URL: {imageUrl}";

                    // Send queue message
                    _logger.LogInformation("Sending queue message...");
                    await _storageService.SendQueueMessageAsync($"Image uploaded directly: {imageFile.FileName}");
                    _logger.LogInformation("Queue message sent successfully");
                }
                else
                {
                    TempData["ErrorMessage"] = "Please select an image file to upload";
                }

                _logger.LogInformation("UploadImage completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadImage");
                TempData["ErrorMessage"] = $"Error uploading image: {ex.Message}";
            }

            return RedirectToAction("Images");
        }

        public async Task<IActionResult> Images()
        {
            try
            {
                // Get images from Azure Storage
                var images = await _storageService.GetImagesAsync();
                return View(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images");
                TempData["ErrorMessage"] = "Error loading images from storage";
                return View(new List<string>());
            }
        }

        // Contract Management
        [HttpGet]
        public IActionResult UploadContract()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadContract(IFormFile contractFile)
        {
            try
            {
                _logger.LogInformation($"Starting UploadContract for: {contractFile?.FileName}");

                if (contractFile != null && contractFile.Length > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".xls", ".ppt", ".pptx" };
                    var fileExtension = Path.GetExtension(contractFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file type. Please upload PDF, Word, Excel, PowerPoint, or Text files.";
                        return RedirectToAction("UploadContract");
                    }

                    // Validate file size (100MB limit)
                    if (contractFile.Length > 100 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must be less than 100MB.";
                        return RedirectToAction("UploadContract");
                    }

                    // Call Azure Function to upload contract
                    _logger.LogInformation("Calling FunctionService.UploadContractAsync...");
                    var result = await _functionService.UploadContractAsync(contractFile);
                    _logger.LogInformation($"FunctionService returned: {result}");

                    try
                    {
                        var response = JsonSerializer.Deserialize<FunctionResponse>(result);
                        if (response?.Success == true)
                        {
                            TempData["SuccessMessage"] = response.Message;
                            _logger.LogInformation($"Success: {response.Message}");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = response?.Message ?? "Failed to upload contract";
                            _logger.LogWarning($"Failed: {response?.Message}");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning($"JSON parse failed, using raw result: {result}");
                        TempData["SuccessMessage"] = result;
                    }

                    // Send queue message
                    _logger.LogInformation("Sending queue message...");
                    await _functionService.SendQueueMessageAsync($"Contract uploaded: {contractFile.FileName}");
                    _logger.LogInformation("Queue message sent successfully");
                }
                else
                {
                    TempData["ErrorMessage"] = "Please select a contract file to upload";
                }

                _logger.LogInformation("UploadContract completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadContract");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Contracts");
        }

        public async Task<IActionResult> Contracts()
        {
            try
            {
                var contracts = await _storageService.GetContractsAsync();
                return View(contracts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contracts");
                TempData["ErrorMessage"] = "Error loading contracts";
                return View(new List<string>());
            }
        }

        public async Task<IActionResult> DownloadContract(string fileName)
        {
            try
            {
                _logger.LogInformation($"Downloading contract: {fileName}");
                var stream = await _storageService.DownloadContractAsync(fileName);
                return File(stream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading contract");
                TempData["ErrorMessage"] = $"Error downloading {fileName}";
                return RedirectToAction("Contracts");
            }
        }

        // Queue Messages
        [HttpGet]
        public IActionResult SendMessage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string message)
        {
            try
            {
                _logger.LogInformation($"Starting SendMessage with: {message}");

                if (!string.IsNullOrEmpty(message))
                {
                    // Call Azure Function to send queue message
                    _logger.LogInformation("Calling FunctionService.SendQueueMessageAsync...");
                    var result = await _functionService.SendQueueMessageAsync(message);
                    _logger.LogInformation($"FunctionService returned: {result}");

                    try
                    {
                        var response = JsonSerializer.Deserialize<FunctionResponse>(result);
                        if (response?.Success == true)
                        {
                            TempData["SuccessMessage"] = response.Message;
                            _logger.LogInformation($"Success: {response.Message}");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = response?.Message ?? "Failed to send message";
                            _logger.LogWarning($"Failed: {response?.Message}");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning($"JSON parse failed, using raw result: {result}");
                        TempData["SuccessMessage"] = result;
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Please enter a message";
                }

                _logger.LogInformation("SendMessage completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessage");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Diagnostic Methods
        [HttpGet]
        public async Task<IActionResult> CheckConfiguration()
        {
            var configInfo = new List<string>();

            // Check Storage Configuration
            var storageConnString = _configuration["AzureStorage:ConnectionString"];
            configInfo.Add($"Storage Connection String: {(string.IsNullOrEmpty(storageConnString) ? "? MISSING" : "? SET")}");

            // Check Functions Configuration
            var functionsBaseUrl = _configuration["AzureFunctions:BaseUrl"];
            var functionsKey = _configuration["AzureFunctions:Key"];

            configInfo.Add($"Functions BaseUrl: {(string.IsNullOrEmpty(functionsBaseUrl) ? "? MISSING" : $"? {functionsBaseUrl}")}");
            configInfo.Add($"Functions Key: {(string.IsNullOrEmpty(functionsKey) ? "? MISSING" : "? SET")}");

            // Test Function Connection
            if (_functionService != null)
            {
                var testResult = await _functionService.TestFunctionConnection();
                configInfo.Add($"Function Connection Test: {testResult}");
            }
            else
            {
                configInfo.Add("? FunctionService is not available");
            }

            // Test Storage Connection
            try
            {
                var stats = await _storageService.GetStorageStatsAsync();
                configInfo.Add($"? Storage Connection: Working (Customers: {stats.CustomerCount}, Images: {stats.ImageCount}, Contracts: {stats.ContractCount}, Queue: {stats.QueueMessageCount})");
            }
            catch (Exception ex)
            {
                configInfo.Add($"? Storage Connection Failed: {ex.Message}");
            }

            return View("ConfigCheck", configInfo);
        }

        [HttpGet]
        public async Task<IActionResult> TestFunctions()
        {
            var results = new List<string>();

            try
            {
                results.Add("?? Starting Function Tests...");

                // Test 1: Simple HTTP call to verify internet connectivity
                results.Add("Testing HTTP connectivity...");
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync("https://www.google.com");
                results.Add($"?? Google test: {(response.IsSuccessStatusCode ? "? SUCCESS" : "? FAILED")} - {response.StatusCode}");

                // Test 2: Function URL reachability
                var functionBaseUrl = _configuration["AzureFunctions:BaseUrl"];
                var functionKey = _configuration["AzureFunctions:Key"];

                if (string.IsNullOrEmpty(functionBaseUrl))
                {
                    results.Add("? Function BaseUrl is missing in configuration");
                    return View("TestResults", results);
                }

                if (string.IsNullOrEmpty(functionKey))
                {
                    results.Add("? Function Key is missing in configuration");
                    return View("TestResults", results);
                }

                results.Add($"Testing Function App reachability: {functionBaseUrl}");
                response = await httpClient.GetAsync(functionBaseUrl);
                results.Add($"?? Function app reachable: {(response.IsSuccessStatusCode ? "? YES" : "? NO")} - {response.StatusCode}");

                // Test 3: Specific function calls
                results.Add("Testing individual functions...");

                // Test AddCustomer function
                var addCustomerUrl = $"{functionBaseUrl}/api/AddCustomer?code={functionKey}";
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent("Test Customer"), "name");
                formData.Add(new StringContent("test@example.com"), "email");
                formData.Add(new StringContent("1234567890"), "phone");

                response = await httpClient.PostAsync(addCustomerUrl, formData);
                results.Add($"?? AddCustomer function: {(response.IsSuccessStatusCode ? "? WORKING" : "? FAILED")} - {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    results.Add($"   Response: {content}");
                }

                // Test SendQueueMessage function
                var queueUrl = $"{functionBaseUrl}/api/SendQueueMessage?code={functionKey}";
                formData = new MultipartFormDataContent();
                formData.Add(new StringContent("Test message from diagnostic"), "message");

                response = await httpClient.PostAsync(queueUrl, formData);
                results.Add($"?? SendQueueMessage function: {(response.IsSuccessStatusCode ? "? WORKING" : "? FAILED")} - {response.StatusCode}");

                results.Add("? All tests completed");
            }
            catch (Exception ex)
            {
                results.Add($"? Error during testing: {ex.Message}");
                results.Add($"   Stack trace: {ex.StackTrace}");
            }

            return View("TestResults", results);
        }

        // Fallback method - Use direct storage if functions fail
        [HttpPost]
        public async Task<IActionResult> AddCustomerDirect(CustomerProfile customer)
        {
            try
            {
                _logger.LogInformation($"Using DIRECT storage for AddCustomer: {customer.Name}");

                customer.RowKey = Guid.NewGuid().ToString();
                await _storageService.AddCustomerAsync(customer);

                await _storageService.SendQueueMessageAsync($"New customer registered: {customer.Name}");

                TempData["SuccessMessage"] = $"Customer {customer.Name} added successfully (Direct Storage)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding customer via direct storage");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Customers");
        }

        [HttpGet]
        public IActionResult QuickConfigCheck()
        {
            var configInfo = new Dictionary<string, string>();

            // Check Storage Configuration
            configInfo["Storage Connection"] = string.IsNullOrEmpty(_configuration["AzureStorage:ConnectionString"])
                ? "? MISSING" : "? SET";

            // Check Functions Configuration
            configInfo["Functions BaseUrl"] = string.IsNullOrEmpty(_configuration["AzureFunctions:BaseUrl"])
                ? "? MISSING" : $"? {_configuration["AzureFunctions:BaseUrl"]}";

            configInfo["Functions Key"] = string.IsNullOrEmpty(_configuration["AzureFunctions:Key"])
                ? "? MISSING" : "? SET";

            return View(configInfo);
        }

        [HttpPost]
        public async Task<IActionResult> UploadImageDirect(IFormFile imageFile)
        {
            try
            {
                _logger.LogInformation($"Using DIRECT storage for UploadImage: {imageFile?.FileName}");

                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await _storageService.UploadImageAsync(imageFile);
                    await _storageService.SendQueueMessageAsync($"Image uploaded: {imageFile.FileName}");

                    TempData["SuccessMessage"] = $"Image {imageFile.FileName} uploaded successfully (Direct Storage)";
                }
                else
                {
                    TempData["ErrorMessage"] = "Please select an image file to upload";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image via direct storage");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Images");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }

    // Helper class to deserialize function responses
    public class FunctionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}