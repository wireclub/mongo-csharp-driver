﻿/* Copyright 2010-2013 10gen Inc.
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

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using NUnit.Framework;

namespace MongoDB.BsonUnitTests.Serialization.Conventions
{
    [TestFixture]
    public class IgnoreExtraElementsConventionsTests
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestApply(bool value)
        {
            var subject = new IgnoreExtraElementsConvention(value);
            var classMap = new BsonClassMap<TestClass>();

            subject.Apply(classMap);

            Assert.AreEqual(value, classMap.IgnoreExtraElements);
        }

        private class TestClass
        {
            public ObjectId Id { get; set; }
        }
    }
}