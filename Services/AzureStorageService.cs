using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace ABCRetailCloudSolution.Services
{
    public class AzureStorageService
    {
        private readonly string _connectionString;
        private readonly TableServiceClient _tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly QueueServiceClient _queueServiceClient;
        private readonly ShareServiceClient _fileServiceClient;
        private readonly ILogger<AzureStorageService> _logger;

        public AzureStorageService(IConfiguration configuration, ILogger<AzureStorageService> logger)
        {
            _connectionString = configuration["AzureStorage:ConnectionString"];
            _tableServiceClient = new TableServiceClient(_connectionString);
            _blobServiceClient = new BlobServiceClient(_connectionString);
            _queueServiceClient = new QueueServiceClient(_connectionString);
            _fileServiceClient = new ShareServiceClient(_connectionString);
            _logger = logger;
        }

        // Table Storage Methods - Customer Management
        public async Task AddCustomerAsync(CustomerProfile customer)
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding customer: {customer.Name}");
                throw;
            }
        }

        public async Task<List<CustomerProfile>> GetCustomersAsync()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();
                return tableClient.Query<CustomerProfile>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return new List<CustomerProfile>();
            }
        }

        public async Task<CustomerProfile> GetCustomerAsync(string rowKey)
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                var customer = await tableClient.GetEntityAsync<CustomerProfile>("Customer", rowKey);
                return customer.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving customer: {rowKey}");
                throw;
            }
        }

        public async Task DeleteCustomerAsync(string rowKey)
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.DeleteEntityAsync("Customer", rowKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting customer: {rowKey}");
                throw;
            }
        }

        // Blob Storage Methods - Image Management
        public async Task<string> UploadImageAsync(IFormFile file)
        {
            try
            {
                _logger.LogInformation($"Starting image upload: {file.FileName}, Size: {file.Length} bytes");

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Container 'product-images' ready");

                var blobName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(file.FileName)}{Path.GetExtension(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(blobName);
                _logger.LogInformation($"Creating blob: {blobName}");

                using var stream = file.OpenReadStream();
                var response = await blobClient.UploadAsync(stream, true);

                _logger.LogInformation($"Image uploaded successfully. Blob URL: {blobClient.Uri}");
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading image: {file.FileName}");
                throw;
            }
        }

        public async Task<List<string>> GetImagesAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving images from blob storage");

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");

                if (!await containerClient.ExistsAsync())
                {
                    _logger.LogInformation("Container 'product-images' does not exist yet");
                    return new List<string>();
                }

                var blobs = containerClient.GetBlobsAsync();
                var imageUrls = new List<string>();

                await foreach (var blob in blobs)
                {
                    var blobClient = containerClient.GetBlobClient(blob.Name);
                    imageUrls.Add(blobClient.Uri.ToString());
                    _logger.LogInformation($"Found image: {blob.Name}");
                }

                _logger.LogInformation($"Retrieved {imageUrls.Count} images from storage");
                return imageUrls;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images from blob storage");
                return new List<string>();
            }
        }

        public async Task<bool> DeleteImageAsync(string blobUrl)
        {
            try
            {
                var blobName = blobUrl.Split('/').Last();
                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                var response = await containerClient.DeleteBlobAsync(blobName);
                return !response.IsError;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image: {blobUrl}");
                return false;
            }
        }

        public async Task<Stream> DownloadImageAsync(string blobUrl)
        {
            try
            {
                var blobName = blobUrl.Split('/').Last();
                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading image: {blobUrl}");
                throw;
            }
        }

        // Queue Storage Methods - Order Processing
        public async Task SendQueueMessageAsync(string message)
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();

                // Encode the message for queue storage
                var encodedMessage = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message));
                await queueClient.SendMessageAsync(encodedMessage);
                _logger.LogInformation($"Queue message sent: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending queue message");
                throw;
            }
        }

        public async Task<List<string>> GetQueueMessagesAsync(int maxMessages = 10)
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();

                var messages = new List<string>();
                var receivedMessages = await queueClient.ReceiveMessagesAsync(maxMessages);

                foreach (var message in receivedMessages.Value)
                {
                    var decodedMessage = System.Text.Encoding.UTF8.GetString(
                        Convert.FromBase64String(message.MessageText));
                    messages.Add(decodedMessage);

                    // Delete the message after processing
                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving queue messages");
                return new List<string>();
            }
        }

        public async Task<int> GetQueueMessageCountAsync()
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();

                var properties = await queueClient.GetPropertiesAsync();
                return properties.Value.ApproximateMessagesCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue count");
                return 0;
            }
        }

        // File Storage Methods - Contract Management
        public async Task UploadContractAsync(IFormFile file)
        {
            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(file.FileName);

                using var stream = file.OpenReadStream();
                await fileClient.CreateAsync(stream.Length);
                await fileClient.UploadAsync(stream);
                _logger.LogInformation($"Contract uploaded: {file.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading contract: {file.FileName}");
                throw;
            }
        }

        public async Task<List<string>> GetContractsAsync()
        {
            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetRootDirectoryClient();
                var files = new List<string>();

                await foreach (var fileItem in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory)
                    {
                        files.Add(fileItem.Name);
                    }
                }

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contracts");
                return new List<string>();
            }
        }

        public async Task<Stream> DownloadContractAsync(string fileName)
        {
            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                var directoryClient = shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                var response = await fileClient.DownloadAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading contract: {fileName}");
                throw;
            }
        }

        public async Task<bool> DeleteContractAsync(string fileName)
        {
            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                var directoryClient = shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                var response = await fileClient.DeleteAsync();
                return !response.IsError;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting contract: {fileName}");
                return false;
            }
        }

        public async Task<bool> ContractExistsAsync(string fileName)
        {
            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                var directoryClient = shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(fileName);

                return await fileClient.ExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking contract existence: {fileName}");
                return false;
            }
        }

        // Utility Methods
        public async Task InitializeStorageAsync()
        {
            try
            {
                // Initialize all storage containers
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();

                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();

                var shareClient = _fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();

                _logger.LogInformation("All Azure Storage components initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing storage");
                throw;
            }
        }

        public async Task<StorageStats> GetStorageStatsAsync()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                var customers = tableClient.Query<CustomerProfile>().ToList();

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                var imageCount = 0;
                await foreach (var blob in containerClient.GetBlobsAsync()) imageCount++;

                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                var queueProperties = await queueClient.GetPropertiesAsync();
                var queueCount = queueProperties.Value.ApproximateMessagesCount;

                var shareClient = _fileServiceClient.GetShareClient("contracts");
                var contractCount = 0;
                var directoryClient = shareClient.GetRootDirectoryClient();
                await foreach (var fileItem in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory) contractCount++;
                }

                return new StorageStats
                {
                    CustomerCount = customers.Count,
                    ImageCount = imageCount,
                    QueueMessageCount = queueCount,
                    ContractCount = contractCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage stats");
                return new StorageStats();
            }
        }
    }

    // Storage statistics model
    public class StorageStats
    {
        public int CustomerCount { get; set; }
        public int ImageCount { get; set; }
        public int QueueMessageCount { get; set; }
        public int ContractCount { get; set; }
    }
}