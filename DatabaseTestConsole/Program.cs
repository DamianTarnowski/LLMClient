using LLMClient.Services;
using LLMClient.Models;
using System.Diagnostics;

Console.WriteLine("=== Database Memory Test Console ===");

// Initialize services
var secureApiKeyService = new SecureApiKeyService();
var databaseService = new DatabaseService(secureApiKeyService);
var memoryService = new DatabaseMemoryService(databaseService);

try
{
    Console.WriteLine("Testing database memory functionality...");
    
    // Test 1: Get initial count
    Console.WriteLine("\n1. Getting initial memory count...");
    var initialMemories = await memoryService.GetAllMemoriesAsync();
    Console.WriteLine($"Initial memories count: {initialMemories.Count}");
    
    // Test 2: Add test memory
    Console.WriteLine("\n2. Adding test memory...");
    var testKey = $"test_key_{DateTime.Now.Ticks}";
    var testValue = "This is a test value from console app";
    var result = await memoryService.UpsertMemoryAsync(testKey, testValue, "test", "console,debug", true);
    Console.WriteLine($"Upsert result: {result}");
    
    // Test 3: Retrieve immediately
    Console.WriteLine("\n3. Retrieving memories immediately after add...");
    var memoriesAfterAdd = await memoryService.GetAllMemoriesAsync();
    Console.WriteLine($"Memories count after add: {memoriesAfterAdd.Count}");
    
    var addedMemory = memoriesAfterAdd.FirstOrDefault(m => m.Key == testKey);
    if (addedMemory != null)
    {
        Console.WriteLine($"✓ Memory found: {addedMemory.Key} = {addedMemory.Value}");
        Console.WriteLine($"  ID: {addedMemory.Id}, Category: {addedMemory.Category}");
        Console.WriteLine($"  Created: {addedMemory.CreatedAt}, Updated: {addedMemory.UpdatedAt}");
    }
    else
    {
        Console.WriteLine("✗ Memory NOT found - this indicates the problem!");
    }
    
    // Test 4: Get by key
    Console.WriteLine("\n4. Getting memory by key...");
    var memoryByKey = await memoryService.GetMemoryByKeyAsync(testKey);
    if (memoryByKey != null)
    {
        Console.WriteLine($"✓ Memory found by key: {memoryByKey.Key} = {memoryByKey.Value}");
    }
    else
    {
        Console.WriteLine("✗ Memory NOT found by key!");
    }
    
    // Test 5: Database file info
    Console.WriteLine("\n5. Database file information...");
    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "llmclient.db3");
    Console.WriteLine($"Database path: {dbPath}");
    Console.WriteLine($"Database file exists: {File.Exists(dbPath)}");
    
    if (File.Exists(dbPath))
    {
        var fileInfo = new FileInfo(dbPath);
        Console.WriteLine($"Database file size: {fileInfo.Length} bytes");
        Console.WriteLine($"Database last modified: {fileInfo.LastWriteTime}");
    }
    
    Console.WriteLine("\n=== Test completed ===");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"\nERROR: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}