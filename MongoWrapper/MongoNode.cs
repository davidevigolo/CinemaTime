using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace MongoWrapper.MongoCore
{
    public delegate void LogEventDelegate(String message);
    public class MongoNode
    {
        protected string connectionString;
        protected string dbName;
        protected IMongoDatabase dataBase;
        public MongoClient Client { get; private set; }
        public event LogEventDelegate Log; 


        public MongoNode(string connectionString, string dbName)
        {
            this.connectionString = connectionString;
            this.dbName = dbName;
        }

        public void Connect()
        {
            Log($"Connecting to {connectionString}...");
            Client = new MongoClient(connectionString);
            Log($"Accessing database {dbName}...");
            dataBase = Client.GetDatabase(dbName);
        }

        public void Use(string dbName)
        {
            Log($"Connecting to {dbName}");
            dataBase = Client.GetDatabase(dbName);
        }

        public IMongoDatabase GetDatabase()
        {
            if (dataBase == null)
                throw new NodeNotConnectedException();

            return dataBase;
        }

        public IMongoCollection<BsonDocument> GetBsonCollection(string name)
        {
            if (dataBase == null)
                throw new NodeNotConnectedException();

            return dataBase.GetCollection<BsonDocument>(name);
        }


    }
}
