﻿/* Copyright 2010 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace MongoDB.DriverOnlineTests.CommandResults {
    [TestFixture]
    public class CollectionStatsResultTests {
        private MongoServer server;
        private MongoDatabase database;
        private MongoCollection<BsonDocument> collection;

        [TestFixtureSetUp]
        public void Setup() {
            server = MongoServer.Create("mongodb://localhost/?safe=true");
            database = server["driveronlinetests"];
            collection = database["test"];
        }

        [Test]
        public void Test() {
            // make sure collection exists and has exactly one document
            collection.RemoveAll();
            collection.Insert(new BsonDocument());

            var result = collection.GetStats();
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("driveronlinetests.test", result.Namespace);
            Assert.AreEqual(1, result.ObjectCount);
            Assert.IsTrue(result.AverageObjectSize > 0.0);
            Assert.IsTrue(result.DataSize > 0);
            Assert.IsTrue(result.ExtentCount > 0);
            Assert.IsTrue(result.IndexCount > 0);
            Assert.IsTrue(result.IndexSizes["_id_"] > 0);
            Assert.IsTrue(result.IndexSizes.ContainsKey("_id_"));
            Assert.IsTrue(result.IndexSizes.Count > 0);
            Assert.IsTrue(result.IndexSizes.Keys.Contains("_id_"));
            Assert.IsTrue(result.IndexSizes.Values.Count() > 0);
            Assert.IsTrue(result.IndexSizes.Values.First() > 0);
            Assert.IsTrue(result.LastExtentSize > 0);
            Assert.IsTrue(result.PaddingFactor > 0.0);
            Assert.IsTrue(result.StorageSize > 0);
            Assert.IsTrue(result.TotalIndexSize > 0);
        }
    }
}
