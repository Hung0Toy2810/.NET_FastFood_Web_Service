using MongoDB.Driver;
namespace LapTrinhWindows.Context
{
    public class MongoDbContext
    {
        public IMongoDatabase Database { get; }

        public MongoDbContext(IConfiguration configuration)
        {
            var mongoSection = configuration.GetSection("MongoDb");
            var connectionString = mongoSection["ConnectionString"];
            var databaseName = mongoSection["Database"];
            var client = new MongoClient(connectionString);
            Database = client.GetDatabase(databaseName);
        }
    }
}