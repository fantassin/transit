﻿// The MIT License (MIT)

// Copyright (c) 2017 Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Itinero.Graphs.Geometric.Shapes;
using Itinero.LocalGeo;
using Itinero.Transit.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace Itinero.Transit.Test.Data
{
    /// <summary>
    /// Shapes db tests.
    /// </summary>
    [TestFixture]
    public class ShapesDbTests
    {
        /// <summary>
        /// Tests adding a shape, sorting and getting the result.
        /// </summary>
        [Test]
        public void TestAddSortGet()
        {
            var shapesDb = new ShapesDb();
            shapesDb.Add(0, 1, new Coordinate(0, 0), new Coordinate(1, 10));
            shapesDb.Add(2, 3, new Coordinate(2, 20), new Coordinate(3, 30));
            shapesDb.Add(4, 5, new Coordinate(4, 40), new Coordinate(5, 50));
            shapesDb.Add(7, 6, new Coordinate(7, 70), new Coordinate(6, 60));
            shapesDb.Add(1, 2, new Coordinate(1, 10), new Coordinate(2, 20));

            shapesDb.Sort();

            var shape = shapesDb.Get(0, 1);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(0, shape[0].Latitude);
            Assert.AreEqual(0, shape[0].Longitude);
            Assert.AreEqual(1, shape[1].Latitude);
            Assert.AreEqual(10, shape[1].Longitude);

            shape = shapesDb.Get(2, 3);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(2, shape[0].Latitude);
            Assert.AreEqual(20, shape[0].Longitude);
            Assert.AreEqual(3, shape[1].Latitude);
            Assert.AreEqual(30, shape[1].Longitude);

            shape = shapesDb.Get(4, 5);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(4, shape[0].Latitude);
            Assert.AreEqual(40, shape[0].Longitude);
            Assert.AreEqual(5, shape[1].Latitude);
            Assert.AreEqual(50, shape[1].Longitude);

            shape = shapesDb.Get(7, 6);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(7, shape[0].Latitude);
            Assert.AreEqual(70, shape[0].Longitude);
            Assert.AreEqual(6, shape[1].Latitude);
            Assert.AreEqual(60, shape[1].Longitude);

            shape = shapesDb.Get(1, 2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(1, shape[0].Latitude);
            Assert.AreEqual(10, shape[0].Longitude);
            Assert.AreEqual(2, shape[1].Latitude);
            Assert.AreEqual(20, shape[1].Longitude);

            shape = shapesDb.Get(5, 4);
            Assert.IsNull(shape);
        }

        /// <summary>
        /// Tests serialize/deserialize.
        /// </summary>
        [Test]
        public void TestSerializeDeserialize()
        {
            var shapesDb = new ShapesDb();
            shapesDb.Add(0, 1, new Coordinate(0, 0), new Coordinate(1, 10));
            shapesDb.Add(2, 3, new Coordinate(2, 20), new Coordinate(3, 30));
            shapesDb.Add(4, 5, new Coordinate(4, 40), new Coordinate(5, 50));
            shapesDb.Add(7, 6, new Coordinate(7, 70), new Coordinate(6, 60));
            shapesDb.Add(1, 2, new Coordinate(1, 10), new Coordinate(2, 20));

            shapesDb.Sort();

            var memoryStream = new MemoryStream();
            shapesDb.Serialize(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            shapesDb = ShapesDb.Deserialize(memoryStream);

            var shape = shapesDb.Get(0, 1);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(0, shape[0].Latitude);
            Assert.AreEqual(0, shape[0].Longitude);
            Assert.AreEqual(1, shape[1].Latitude);
            Assert.AreEqual(10, shape[1].Longitude);

            shape = shapesDb.Get(2, 3);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(2, shape[0].Latitude);
            Assert.AreEqual(20, shape[0].Longitude);
            Assert.AreEqual(3, shape[1].Latitude);
            Assert.AreEqual(30, shape[1].Longitude);

            shape = shapesDb.Get(4, 5);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(4, shape[0].Latitude);
            Assert.AreEqual(40, shape[0].Longitude);
            Assert.AreEqual(5, shape[1].Latitude);
            Assert.AreEqual(50, shape[1].Longitude);

            shape = shapesDb.Get(7, 6);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(7, shape[0].Latitude);
            Assert.AreEqual(70, shape[0].Longitude);
            Assert.AreEqual(6, shape[1].Latitude);
            Assert.AreEqual(60, shape[1].Longitude);

            shape = shapesDb.Get(1, 2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(1, shape[0].Latitude);
            Assert.AreEqual(10, shape[0].Longitude);
            Assert.AreEqual(2, shape[1].Latitude);
            Assert.AreEqual(20, shape[1].Longitude);

            shape = shapesDb.Get(5, 4);
            Assert.IsNull(shape);
        }

        /// <summary>
        /// Tests enumerating shapes.
        /// </summary>
        [Test]
        public void TestEnumerate()
        {
            var shapesDb = new ShapesDb();
            shapesDb.Add(0, 1, new Coordinate(0, 0), new Coordinate(1, 10));
            shapesDb.Add(2, 3, new Coordinate(2, 20), new Coordinate(3, 30));
            shapesDb.Add(4, 5, new Coordinate(4, 40), new Coordinate(5, 50));
            shapesDb.Add(7, 6, new Coordinate(7, 70), new Coordinate(6, 60));
            shapesDb.Add(1, 2, new Coordinate(1, 10), new Coordinate(2, 20));

            shapesDb.Sort();

            var shapes = new List<Tuple<uint, uint, ShapeBase>>();
            var enumerator = shapesDb.GetEnumerator();
            while (enumerator.MoveNext())
            {
                shapes.Add(new Tuple<uint, uint, ShapeBase>(enumerator.Stop1, enumerator.Stop2, enumerator.Shape));
            }

            Assert.AreEqual(5, shapes.Count);

            var shapeTuple = shapes[0];
            var shape = shapeTuple.Item3;
            Assert.AreEqual(0, shapeTuple.Item1);
            Assert.AreEqual(1, shapeTuple.Item2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(0, shape[0].Latitude);
            Assert.AreEqual(0, shape[0].Longitude);
            Assert.AreEqual(1, shape[1].Latitude);
            Assert.AreEqual(10, shape[1].Longitude);

            shapeTuple = shapes[1];
            shape = shapeTuple.Item3;
            Assert.AreEqual(1, shapeTuple.Item1);
            Assert.AreEqual(2, shapeTuple.Item2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(1, shape[0].Latitude);
            Assert.AreEqual(10, shape[0].Longitude);
            Assert.AreEqual(2, shape[1].Latitude);
            Assert.AreEqual(20, shape[1].Longitude);

            shapeTuple = shapes[2];
            shape = shapeTuple.Item3;
            Assert.AreEqual(2, shapeTuple.Item1);
            Assert.AreEqual(3, shapeTuple.Item2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(2, shape[0].Latitude);
            Assert.AreEqual(20, shape[0].Longitude);
            Assert.AreEqual(3, shape[1].Latitude);
            Assert.AreEqual(30, shape[1].Longitude);

            shapeTuple = shapes[3];
            shape = shapeTuple.Item3;
            Assert.AreEqual(4, shapeTuple.Item1);
            Assert.AreEqual(5, shapeTuple.Item2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(4, shape[0].Latitude);
            Assert.AreEqual(40, shape[0].Longitude);
            Assert.AreEqual(5, shape[1].Latitude);
            Assert.AreEqual(50, shape[1].Longitude);

            shapeTuple = shapes[4];
            shape = shapeTuple.Item3;
            Assert.AreEqual(7, shapeTuple.Item1);
            Assert.AreEqual(6, shapeTuple.Item2);
            Assert.IsNotNull(shape);
            Assert.AreEqual(2, shape.Count);
            Assert.AreEqual(7, shape[0].Latitude);
            Assert.AreEqual(70, shape[0].Longitude);
            Assert.AreEqual(6, shape[1].Latitude);
            Assert.AreEqual(60, shape[1].Longitude);
        }
    }
}