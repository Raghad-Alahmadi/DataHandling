# Product API

This .NET API provides endpoints for retrieving paginated product data with various sorting options. The system includes an automated product sorter that runs as a hosted service to prepare pre-sorted data files for efficient retrieval.

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Setup Instructions](#setup-instructions)
- [API Endpoints](#api-endpoints)
- [Implementation Details](#implementation-details)
  - [Product Controller](#product-controller)
  - [Product Sorter Hosted Service](#product-sorter-hosted-service)
- [Testing](#testing)
- [Performance Considerations](#performance-considerations)

## Features

- **RESTful API** for retrieving products with pagination
- **Background service** that pre-sorts product data for optimal query performance
- **Support for multiple sort criteria** (id, name, price)
- **Efficient file streaming** to handle large datasets
- **Input validation** to ensure proper API usage

## Architecture

The application consists of two main components:

1. **API Controller**: Handles HTTP requests and serves pre-sorted product data
2. **ProductSorterHostedService**: Background service that runs on startup to prepare sorted product files

## Setup Instructions

1. Clone the repository
2. Ensure you have .NET Core 6.0+ installed
3. Make sure the `products.txt` file exists in the designated data directory
4. Run the application (`dotnet run`)
5. The ProductSorterHostedService will automatically run on startup to generate sorted files

## API Endpoints

### Get Products

```
GET /api/products
```

**Query Parameters:**
- `pageNumber` (optional, default: 1) - The page number to retrieve
- `pageSize` (optional, default: 10) - Number of products per page
- `sortBy` (optional, default: "id") - Sort criterion ("id", "name", or "price")

**Success Response:**
- Status Code: 200 OK
- Content: Array of product objects

**Error Responses:**
- Status Code: 400 Bad Request
  - When pageNumber or pageSize is less than 1
- Status Code: 404 Not Found
  - When the sorted file cannot be found

## Implementation Details

### Product Controller

The `ProductsController` class implements a GET endpoint that:

1. Accepts and validates pagination and sorting parameters
2. Determines which pre-sorted file to use based on the `sortBy` parameter
3. Calculates the correct offset based on page number and size
4. Streams only the required portion of the file for efficiency
5. Returns paginated results in the HTTP response


### Product Sorter Hosted Service

The `ProductSorterHostedService` implements `IHostedService` and:

1. Runs automatically when the application starts
2. Reads the source product data file
3. Creates three sorted versions of the file (by id, name, and price)
4. Processes data in batches to optimize memory usage
5. Ensures all file operations are properly handled



## Testing

To test the API:

1. Start the application
2. Use a tool like Postman or curl to make requests to the API
3. Example request: `GET http://localhost:5000/api/products?pageNumber=2&pageSize=5&sortBy=price`

## Performance Considerations

- The application uses file streaming to efficiently handle large datasets
- Pre-sorting data files eliminates the need for in-memory sorting during API requests
- Batch processing in the sorter service prevents memory issues with large files
- File I/O operations are optimized to balance memory usage and processing speed
