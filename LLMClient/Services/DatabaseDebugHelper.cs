using LLMClient.Models;
using System.Diagnostics;

namespace LLMClient.Services
{
    public static class DatabaseDebugHelper
    {
        public static async Task<string> TestMemoryPersistenceAsync(IMemoryService memoryService)
        {
            var testResults = new List<string>();
            var testKey = $"debug_test_{DateTime.Now.Ticks}";
            var testValue = "This is a test value for debugging";
            
            try
            {
                testResults.Add("=== MEMORY PERSISTENCE DEBUG TEST ===");
                
                // Test 1: Count existing memories
                var initialMemories = await memoryService.GetAllMemoriesAsync();
                testResults.Add($"Initial memory count: {initialMemories.Count}");
                
                // Test 2: Add new memory
                testResults.Add($"Adding test memory: {testKey} = {testValue}");
                var addResult = await memoryService.UpsertMemoryAsync(testKey, testValue, "debug", "test,debug", true);
                testResults.Add($"Add result: {addResult}");
                
                // Test 3: Retrieve immediately after adding
                var memoriesAfterAdd = await memoryService.GetAllMemoriesAsync();
                testResults.Add($"Memory count after add: {memoriesAfterAdd.Count}");
                
                var addedMemory = memoriesAfterAdd.FirstOrDefault(m => m.Key == testKey);
                if (addedMemory != null)
                {
                    testResults.Add($"✓ Memory found immediately after add: {addedMemory.Key} = {addedMemory.Value}");
                    testResults.Add($"  ID: {addedMemory.Id}, Category: {addedMemory.Category}, Tags: {addedMemory.Tags}");
                    testResults.Add($"  Created: {addedMemory.CreatedAt}, Updated: {addedMemory.UpdatedAt}");
                }
                else
                {
                    testResults.Add("✗ Memory NOT found immediately after add - THIS IS THE PROBLEM!");
                }
                
                // Test 4: Get specific memory by key
                var memoryByKey = await memoryService.GetMemoryByKeyAsync(testKey);
                if (memoryByKey != null)
                {
                    testResults.Add($"✓ Memory found by key: {memoryByKey.Key} = {memoryByKey.Value}");
                }
                else
                {
                    testResults.Add("✗ Memory NOT found by key - THIS IS THE PROBLEM!");
                }
                
                // Test 5: Clean up - delete test memory
                if (addedMemory != null)
                {
                    await memoryService.DeleteMemoryAsync(addedMemory.Id);
                    testResults.Add($"Test memory deleted (ID: {addedMemory.Id})");
                }
                
                testResults.Add("=== DEBUG TEST COMPLETED ===");
            }
            catch (Exception ex)
            {
                testResults.Add($"ERROR during test: {ex.Message}");
                testResults.Add($"Stack trace: {ex.StackTrace}");
            }
            
            var result = string.Join("\n", testResults);
            Debug.WriteLine(result);
            return result;
        }
        
        public static async Task<string> CheckDatabaseStateAsync(DatabaseService databaseService)
        {
            var results = new List<string>();
            
            try
            {
                results.Add("=== DATABASE STATE CHECK ===");
                
                // Get database info
                var encryptionInfo = await databaseService.GetEncryptionInfoAsync();
                results.Add($"Encryption status: {encryptionInfo}");
                
                var appInfo = await databaseService.GetApplicationInfoAsync();
                results.Add($"Application ID: {appInfo}");
                
                // Check if database is initialized
                var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "llmclient.db3");
                results.Add($"Database path: {dbPath}");
                results.Add($"Database file exists: {File.Exists(dbPath)}");
                
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    results.Add($"Database file size: {fileInfo.Length} bytes");
                    results.Add($"Database last modified: {fileInfo.LastWriteTime}");
                }
                
                results.Add("=== DATABASE STATE CHECK COMPLETED ===");
            }
            catch (Exception ex)
            {
                results.Add($"ERROR during database state check: {ex.Message}");
            }
            
            var result = string.Join("\n", results);
            Debug.WriteLine(result);
            return result;
        }
    }
}