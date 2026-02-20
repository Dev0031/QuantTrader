using QuantTrader.TestInfrastructure.Fixtures;

// Re-declare collections here so xUnit can discover them in this assembly.
// The actual fixture classes live in QuantTrader.TestInfrastructure.

[CollectionDefinition("Redis")]
public class RedisTestCollection : ICollectionFixture<RedisFixture> { }

[CollectionDefinition("PostgreSql")]
public class PostgreSqlTestCollection : ICollectionFixture<PostgreSqlFixture> { }
