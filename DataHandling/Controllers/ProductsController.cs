using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataHandling.Models;

namespace DataHandling.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(ILogger<ProductsController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Gets a paginated list of products with optional sorting
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="sortBy">Sort field: "id", "name", or "price"</param>
        /// <returns>A paginated list of products</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Product>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "id")
        {
            try
            {
                // 1. Validate input parameters
                if (pageNumber < 1 || pageSize < 1)
                {
                    return BadRequest("Page number and size must be greater than zero.");
                }

                // Cap the page size to a reasonable limit
                const int maxPageSize = 100;
                if (pageSize > maxPageSize)
                {
                    _logger.LogWarning($"Page size {pageSize} exceeded maximum limit. Using {maxPageSize} instead.");
                    pageSize = maxPageSize;
                }

                // 2. Determine which sorted file to use
                string fileName;
                switch (sortBy.ToLower())
                {
                    case "name":
                        fileName = "products_sorted_by_name.txt";
                        break;
                    case "price":
                        fileName = "products_sorted_by_price.txt";
                        break;
                    case "id":
                    default:
                        fileName = "products_sorted_by_id.txt";
                        break;
                }

                // 3. Build the file path
                string filePath = Path.Combine(_environment.ContentRootPath, "Data", fileName);
                _logger.LogInformation($"Reading products from file: {filePath}");

                // 4. Check if the file exists
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogError($"Sorted file not found: {filePath}");
                    return NotFound("Sorted file not found.");
                }

                // 5. Calculate how many products to skip
                int skip = (pageNumber - 1) * pageSize;

                // 6. Stream the file and get the relevant products
                var products = new List<Product>();
                int currentIndex = 0;
                int added = 0;

                foreach (var line in System.IO.File.ReadLines(filePath))
                {
                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Try to parse the product
                    try
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            // Only process if we're in the correct range
                            if (currentIndex >= skip && added < pageSize)
                            {
                                int id = int.Parse(parts[0]);
                                string name = parts[1];
                                decimal price = decimal.Parse(parts[2]);

                                products.Add(new Product(id, name, price));
                                added++;
                            }

                            currentIndex++;

                            // If we've added all the products we need, we can stop reading
                            if (added >= pageSize)
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to parse product line: {line}");
                    }
                }

                // 7. Check if no products were found for the requested page
                if (products.Count == 0 && pageNumber > 1)
                {
                    _logger.LogWarning($"No products found for page {pageNumber} with page size {pageSize}");
                    return NotFound($"No products found for page {pageNumber}");
                }

                // 8. Return the products with pagination information
                Response.Headers.Add("X-Total-Count", currentIndex.ToString());
                Response.Headers.Add("X-Page-Number", pageNumber.ToString());
                Response.Headers.Add("X-Page-Size", pageSize.ToString());
                Response.Headers.Add("X-Total-Pages", ((int)Math.Ceiling(currentIndex / (double)pageSize)).ToString());

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing product data.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }
    }
}
