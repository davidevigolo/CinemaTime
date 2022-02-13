using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MongoWrapper.MongoCore
{
    public static class MongoIO
    {

        public static async Task<int> InsertJSONAsync(this IMongoCollection<BsonDocument> collection, BsonDocument document)
        {
            var result = -1;
            try
            {
                await collection.InsertOneAsync(document);
                result = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }

            return result;
        }

        public static int InsertJSON(this IMongoCollection<BsonDocument> collection, BsonDocument document)
        {
            var result = -1;
            try
            {
                collection.InsertOne(document);
                result = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }

            return result;
        }

        /**public static async Task<BsonDocument> GetBsonDocumentAsync(string collectionName, MongoNode mongoConnection, int dataFormat)
        {
            await Task.Run(() => {
                var filter = Builders<BsonDocument>.Filter.Eq("dataFormat", dataFormat);
                var collection = mongoConnection.GetBsonCollection(collectionName);
                var document = collection.Find(filter).FirstOrDefault();
                return document;
            });

            return null;
        }**/

        public static async Task<BsonDocument> GetBsonDocumentAsync(this IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter)
        {
            var document = await collection.FindAsync(filter);
            return await document.FirstAsync<BsonDocument>();
        }

        /**public static BsonDocument GetBsonDocument(string collectionName, MongoNode mongoConnection,int dataFormat) // query
        {
            try
            {
                var filter = Builders<BsonDocument>.Filter.Eq("dataFormat", dataFormat);
                var collection = mongoConnection.GetBsonCollection(collectionName);
                var document = collection.Find(filter).Limit(1).Single();
                return document;
            }catch(InvalidOperationException)
            {
                return null;
            }
        }**/

        /**public static BsonDocument GetBsonDocument(string collectionName, MongoNode mongoConnection, int dataFormat, int id)
        {
            var filter = Builders<BsonDocument>.Filter.And(new BsonDocument("dataFormat",dataFormat), new BsonDocument("id",id));
            var collection = mongoConnection.GetBsonCollection(collectionName);
            var document = collection.Find(filter).Limit(1).Single();
            return document;
        }**/

        /**public static BsonDocument GetBsonDocument(string collectionName, MongoNode mongoConnection, FilterDefinition<BsonDocument> filter, int id)
        {
            var collection = mongoConnection.GetBsonCollection(collectionName);
            var document = collection.Find(filter).Limit(1).Single();
            return document;
        }**/

        public static BsonDocument GetBsonDocument(this IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter)
        {
            try
            {
                var document = collection.Find(filter).Limit(1).Single();
                return document;
            }
            catch(InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            }
            return null;
        }
        public static List<BsonDocument> GetBsonDocuments(this IMongoCollection<BsonDocument> collection,FilterDefinition<BsonDocument> filter, int limit)
        {
            var documents = collection.Find(filter).Limit(limit);
            return documents.ToList();
        }
        public async static Task<List<BsonDocument>> GetBsonDocumentsAsync(this IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, int limit)
        {
            var documents = await collection.FindAsync<BsonDocument>(filter);
            return await documents.ToListAsync();
        }
        /*public static string GetCollectionCorrectName(string citta)
        {
            switch(citta.ToLower()){
                case "bari":
                    return "TabelleBari";
                case "napoli":
                    return "TabelleNapoli";
                case "firenze":
                    return "TabelleFirenze";
                case "genova":
                    return "TabelleGenova";
                case "cagliari":
                    return "TabelleCagliari";
                case "roma":
                    return "TabelleRoma";
                case "palermo":
                    return "TabellePalermo";
                case "torino":
                    return "TabelleTorino";
                case "venezia":
                    return "TabelleVenezia";
                case "milano":
                    return "TabelleMilano";
                default:
                    throw new ArgumentException("Il nome di città " + citta + " non è valido");

            }
        }*/
    }
}
