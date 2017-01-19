﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2016 Abelshausen Ben
// 
// This file is part of Itinero.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Itinero. If not, see <http://www.gnu.org/licenses/>.

using GTFS;
using GTFS.IO;
using Itinero.Osm.Vehicles;
using Itinero.Transit.Data;
using Itinero.Transit.GTFS;
using Itinero.Transit.Test.Functional.Staging;
using System;
using Itinero.Transit.Osm.Data;
using System.IO;

namespace Itinero.Transit.Test.Functional
{
    class Program
    {
        static void Main(string[] args)
        {
            Logging.Logger.LogAction = (origin, level, message, parameters) =>
            {
                Console.WriteLine(string.Format("[{0}] {1} - {2}", origin, level, message));
            };
            
            // download and extract test-data.
            Console.WriteLine("Downloading Belgium...");
            Download.DownloadBelgiumAll();

            // build routerdb and save the result.
            var routerDb = Staging.RouterDbBuilder.BuildBelgium();
            var router = new Router(routerDb);

            Console.WriteLine("Downloading NMBS GTFS...");
            Download.DownloadNMBS();

            Console.WriteLine("Loading NMBS data...");
            var reader = new GTFSReader<GTFSFeed>(false);
            var feed = reader.Read(new GTFSDirectorySource(@"NMBS"));
            var transitDb = new TransitDb();
            var db = new MultimodalDb(routerDb, transitDb);
            db.TransitDb.LoadFrom(feed);
            db.TransitDb.SortConnections(DefaultSorting.DepartureTime, null);
            db.TransitDb.AddTransfersDb(Vehicle.Pedestrian.Fastest(), 100);
            db.AddStopLinksDb(Vehicle.Pedestrian.Fastest(), maxDistance: 100);

            var transitRouter = new MultimodalRouter(db, Vehicle.Pedestrian.Fastest());

            // run tests.
            Runner.Test(transitRouter, "Itinero.Transit.Test.Functional.test_data.belgium.test1.geojson");
            Runner.Test(transitRouter, "Itinero.Transit.Test.Functional.test_data.belgium.test2.geojson");
            Runner.Test(transitRouter, "Itinero.Transit.Test.Functional.test_data.belgium.test3.geojson");

            Console.WriteLine("Done!");
            Console.ReadLine();
        }
    }
}