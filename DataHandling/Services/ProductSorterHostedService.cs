using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataHandling.Models;

namespace DataHandling.Services
{
    public class ProductSorterHostedService : IHostedService
    {
        private readonly ILogger<ProductSorterHostedService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly string _targetDirectoryPath;
        private readonly int _batchSize = 1000;

        public ProductSorterHostedService(ILogger<ProductSorterHostedService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
            _targetDirectoryPath = Path.Combine(environment.ContentRootPath, "Data");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Product Sorter Hosted Service is starting.");

            try
            {
                // Ensure target directory exists
                Directory.CreateDirectory(_targetDirectoryPath);

                // Find the product file
                string sourceFilePath = FindProductFile();
                if (string.IsNullOrEmpty(sourceFilePath))
                {
                    _logger.LogError("Could not find product file in any expected location.");
                    return;
                }

                _logger.LogInformation($"Source file path: {sourceFilePath}");
                _logger.LogInformation($"Target directory path: {_targetDirectoryPath}");

                await SortProductsAsync(sourceFilePath, cancellationToken);
                _logger.LogInformation("Product sorting completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sorting products.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Product Sorter Hosted Service is stopping.");
            return Task.CompletedTask;
        }

        private string FindProductFile()
        {
            // List of possible file paths to check
            var possiblePaths = new List<string>
            {
                Path.Combine(_environment.ContentRootPath, "product.txt"),
                Path.Combine(_environment.ContentRootPath, "Data", "product.txt"),
            };

            // Try to find the file in any of these locations
            foreach (var path in possiblePaths)
            {
                _logger.LogInformation($"Checking for product file at: {path}");
                if (File.Exists(path))
                {
                    _logger.LogInformation($"Found product file at: {path}");
                    return path;
                }
            }

            // If we get here, we couldn't find the file
            return null;
        }

        private async Task SortProductsAsync(string sourceFilePath, CancellationToken cancellationToken)
        {
            // Define output file paths
            string filePathById = Path.Combine(_targetDirectoryPath, "products_sorted_by_id.txt");
            string filePathByName = Path.Combine(_targetDirectoryPath, "products_sorted_by_name.txt");
            string filePathByPrice = Path.Combine(_targetDirectoryPath, "products_sorted_by_price.txt");

            _logger.LogInformation($"Output files will be created at:");
            _logger.LogInformation($"By ID: {filePathById}");
            _logger.LogInformation($"By Name: {filePathByName}");
            _logger.LogInformation($"By Price: {filePathByPrice}");

            // Initialize file streams
            using (StreamWriter writerById = new StreamWriter(filePathById, false))
            using (StreamWriter writerByName = new StreamWriter(filePathByName, false))
            using (StreamWriter writerByPrice = new StreamWriter(filePathByPrice, false))
            {
                var batch = new List<Product>();

                // Read source file line by line
                using (StreamReader reader = new StreamReader(sourceFilePath))
                {
                    string line;
                    int lineCount = 0;
                    int successCount = 0;

                    while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                    {
                        lineCount++;

                        // Skip header lines or empty lines
                        if (string.IsNullOrWhiteSpace(line) || !char.IsDigit(line[0]))
                            continue;

                        try
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                int id = int.Parse(parts[0]);
                                string name = parts[1];
                                decimal price = decimal.Parse(parts[2]);

                                batch.Add(new Product(id, name, price));
                                successCount++;

                                // Process batch when it reaches the batch size
                                if (batch.Count >= _batchSize)
                                {
                                    await WriteBatchAsync(batch, writerById, writerByName, writerByPrice);
                                    batch.Clear();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to parse product line {lineCount}: {line}");
                        }
                    }

                    _logger.LogInformation($"Processed {lineCount} lines, successfully parsed {successCount} products");

                    // Process any remaining records in the batch
                    if (batch.Count > 0)
                    {
                        await WriteBatchAsync(batch, writerById, writerByName, writerByPrice);
                        _logger.LogInformation($"Wrote final batch of {batch.Count} products");
                    }
                }
            }

            // Verify the files were created
            if (File.Exists(filePathById) && File.Exists(filePathByName) && File.Exists(filePathByPrice))
            {
                _logger.LogInformation("All sorted files were created successfully.");
            }
            else
            {
                _logger.LogError("Failed to create one or more sorted files.");
            }
        }

        private async Task WriteBatchAsync(
            List<Product> batch,
            StreamWriter writerById,
            StreamWriter writerByName,
            StreamWriter writerByPrice)
        {
            // Sort and write by Id
            foreach (var product in batch.OrderBy(p => p.Id))
            {
                await writerById.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }

            // Sort and write by Name
            foreach (var product in batch.OrderBy(p => p.Name).ThenBy(p => p.Id))
            {
                await writerByName.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }

            // Sort and write by Price
            foreach (var product in batch.OrderBy(p => p.Price).ThenBy(p => p.Id))
            {
                await writerByPrice.WriteLineAsync($"{product.Id},{product.Name},{product.Price}");
            }

            // Ensure data is written to the files
            await writerById.FlushAsync();
            await writerByName.FlushAsync();
            await writerByPrice.FlushAsync();
        }
    }
}
