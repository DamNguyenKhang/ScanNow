namespace ScanNow.Tests;

/// <summary>
/// Serializes all database tests so they don't compete for the same connection.
/// xUnit runs [Collection("DatabaseTests")] classes sequentially.
/// </summary>
[CollectionDefinition("DatabaseTests", DisableParallelization = true)]
public class DatabaseTestCollection { }
