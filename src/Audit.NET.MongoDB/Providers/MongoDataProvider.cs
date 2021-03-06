using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Audit.Core;

namespace Audit.MongoDB.Providers
{
    /// <summary>
    /// Mongo DB data access
    /// </summary>
    /// <remarks>
    /// Settings:
    /// - ConnectionString: Mongo connection string
    /// - Database: Database name
    /// - Collection: Collection name
    /// </remarks>
    public class MongoDataProvider : AuditDataProvider
    {
        private string _connectionString = "mongodb://localhost:27017";
        private string _database = "Audit";
        private string _collection = "Event";

        static MongoDataProvider()
        {
            ConfigureBsonMapping();
        }

        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }

        public string Database
        {
            get { return _database; }
            set { _database = value; }
        }

        public string Collection
        {
            get { return _collection; }
            set { _collection = value; }
        }

        private static void ConfigureBsonMapping()
        {
            var pack = new ConventionPack();
            pack.Add(new IgnoreIfNullConvention(true));
            ConventionRegistry.Register("Ignore null properties for AuditEvent", pack, type => type == typeof(AuditEvent));

            BsonClassMap.RegisterClassMap<AuditTarget>(cm =>
            {
                cm.AutoMap();
                cm.MapProperty(x => x.SerializedOld).SetElementName("Old");
                cm.MapProperty(x => x.SerializedNew).SetElementName("New");
            });

            BsonClassMap.RegisterClassMap<AuditEvent>(cm =>
            {
               cm.AutoMap();
               cm.MapExtraElementsField(c => c.CustomFields);
            });
        }

        public override object InsertEvent(AuditEvent auditEvent)
        {
            var db = GetDatabase();
            var col = db.GetCollection<BsonDocument>(_collection);
            var doc = auditEvent.ToBsonDocument();
            FixDocumentElementNames(doc);
            col.InsertOne(doc);
            return (BsonObjectId)doc["_id"];
        }

        public override void ReplaceEvent(object eventId, AuditEvent auditEvent)
        {
            var db = GetDatabase();
            var col = db.GetCollection<BsonDocument>(_collection);
            var doc = auditEvent.ToBsonDocument();
            FixDocumentElementNames(doc);
            col.ReplaceOne(d => d["_id"] == (BsonObjectId)eventId, doc);
        }

        /// <summary>
        /// Fixes the document Element Names (avoid using dots '.' and starting with '$').
        /// </summary>
        /// <param name="document">The document to fix.</param>
        private void FixDocumentElementNames(BsonDocument document)
        {
            var toRename = new List<Tuple<string, BsonValue, string>>();
            foreach (var elem in document)
            {
                if (elem.Name.Contains(".") || elem.Name.StartsWith("$"))
                {
                    var value = elem.Value;
                    var name = elem.Name.Replace('.', '_');
                    if (name.StartsWith("$"))
                    {
                        name = "_" + name.Substring(1);
                    }
                    toRename.Add(new Tuple<string, BsonValue, string>(elem.Name, value, name));
                }
                if (elem.Value != null)
                {
                    if (elem.Value.IsBsonDocument)
                    {
                        FixDocumentElementNames(elem.Value as BsonDocument);
                    }
                    else if (elem.Value.IsBsonArray)
                    {
                        foreach (var sub in (elem.Value as BsonArray))
                        {
                            if (sub.IsBsonDocument)
                            {
                                FixDocumentElementNames(sub as BsonDocument);
                            }
                        }
                    }
                }
            }
            foreach (var x in toRename)
            {
                document.Remove(x.Item1);
                document.Add(new BsonElement(x.Item3, x.Item2));
            }
        }

        public override object Serialize<T>(T value)
        {
            // if can be converted to bsonvalue, return the value
            try
            {
                BsonValue bsonValue;
                if (BsonTypeMapper.TryMapToBsonValue(value, out bsonValue))
                {
                    return value;
                }
            }
            catch
            {
                // ignored. TryMapToBsonValue can throw exception (i.e. when the type is an array of objects that cannot be mapped to a bsonvalue)
            }
            return value.ToBsonDocument(typeof(object));
        }

        private void TestConnection()
        {
            var db = GetDatabase();
            var test = db.RunCommand((Command<BsonDocument>)"{ping:1}");
            
            if (test["ok"].ToInt64() != 1)
            {
                throw new Exception("Can't connect to Audit Mongo Database.");
            }
        }

        private IMongoDatabase GetDatabase()
        {
            var client = new MongoClient(_connectionString);
            var db = client.GetDatabase(_database);
            return db;
        }
    }
}