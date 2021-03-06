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

using NUnit.Framework;
using Itinero.Transit.Algorithms.OneToOne;
using Itinero.Transit.Data;
using System;

namespace OsmSharp.Transit.Test.Algorithms.OneToOne
{
    /// <summary>
    /// A test fixture for the profile search algorithm.
    /// </summary>
    [TestFixture]
    public class ProfileSearchTests
    {
        /// <summary>
        /// Tests a successful one-hop with a one-connection db.
        /// 
        /// Departure (0)@00:50:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @01:00          @01:40
        /// 
        /// </summary>
        [Test]
        public void TestOneHop()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 3600, 3600 + 40 * 60);
            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 00, 50, 00);
            var algorithm = new ProfileSearch(db, departureTime,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 50 * 60);
            algorithm.SetTargetStop(1, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(1, arrivalStops[2]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(3600 * 01 + 40 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(50 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 10, 01, 40, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var profiles = algorithm.GetStopProfiles(1);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 50 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.TripId);
            Assert.AreEqual(0, connections.DepartureStop);
            profiles = algorithm.GetStopProfiles(0);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(3600, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests an unsuccessful one-hop with a one-connection db.
        /// 
        /// Departure (0)@08:30:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @08:00          @08:10
        /// 
        /// </summary>
        [Test]
        public void TestOneHopUnsuccessful()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var algorithm = new ProfileSearch(db, new DateTime(2017, 05, 10, 08, 30, 00),
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 08 * 3600 + 30 * 60);
            algorithm.SetTargetStop(1, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsFalse(algorithm.HasSucceeded);
        }

        /// <summary>
        /// Tests a successful two-hop with a two-connection db.
        /// 
        /// Departure (0)@07:30:00
        /// 
        ///   (0)-->---0--->--(1)-->---0--->--(2)
        /// @08:00          @08:10          @08:20
        /// 
        /// </summary>
        [Test]
        public void TestTwoHopsSuccessful()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddStop(2, 2, 2);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.AddConnection(1, 2, 0, 8 * 3600 + 11 * 60, 8 * 3600 + 20 * 60);
            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            var algorithm = new ProfileSearch(db, departureTime,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(2, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(2, arrivalStops[2]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 20 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(50 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 20, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var precedingStop = Itinero.Transit.Constants.NoStopId;
            var transfers = Itinero.Transit.Constants.NoTransfers;

            // get stop 2 profiles.
            var profiles = algorithm.GetStopProfiles(2);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(1, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 50 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.TripId);
            Assert.AreEqual(1, connections.DepartureStop);

            // get preceding profile and check if stop 1 profile.
            profile = algorithm.GetPreceding(profiles, 2, out precedingStop, out transfers);
            Assert.AreEqual(2, transfers);
            Assert.AreEqual(1, precedingStop);
            profiles = algorithm.GetStopProfiles(precedingStop);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);
            Assert.AreEqual(0, profile.PreviousConnectionId);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.DepartureStop);
            Assert.AreEqual(0, connections.TripId);

            // get preceding profile and check if stop 0 profile.
            profile = algorithm.GetPreceding(profiles, transfers, out precedingStop, out transfers);
            Assert.AreEqual(0, transfers);
            Assert.AreEqual(0, precedingStop);
            profiles = algorithm.GetStopProfiles(precedingStop);
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests a successful two-hop, one transfer with a two-connection db.
        /// <summary>
        /// 
        /// Departure (0)@07:30:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @08:00          @08:10      
        /// 
        ///                   (1)-->---1--->--(2)
        ///                 @08:15          @08:25      
        /// </summary>
        /// </summary>
        [Test]
        public void TestTwoHopsOneTransferSuccessful()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddStop(2, 2, 2);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.AddConnection(1, 2, 1, 8 * 3600 + 15 * 60, 8 * 3600 + 25 * 60);
            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            var algorithm = new ProfileSearch(db, departureTime,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(2, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(5, arrivalStops.Count);
            Assert.AreEqual(2, arrivalStops[4]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(5, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 25 * 60, arrivalProfiles[4].Seconds);
            Assert.AreEqual(55 * 60, algorithm.Duration(4));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 25, 00), algorithm.ArrivalTime(4));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var precedingStop = Itinero.Transit.Constants.NoStopId;
            var transfers = Itinero.Transit.Constants.NoTransfers;

            // get stop 2 profiles.
            var profiles = algorithm.GetStopProfiles(2);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[3];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[4];
            Assert.AreEqual(1, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 55 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(1, connections.TripId);
            Assert.AreEqual(1, connections.DepartureStop);

            // get preceding profile and check if stop 1 profile.
            profile = algorithm.GetPreceding(profiles, 4, out precedingStop, out transfers);
            Assert.AreEqual(2, transfers);
            Assert.AreEqual(1, precedingStop);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);
            Assert.AreEqual(0, profile.PreviousConnectionId);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.DepartureStop);
            Assert.AreEqual(0, connections.TripId);
            profiles = algorithm.GetStopProfiles(precedingStop);

            // get preceding profile and check if stop 0 profile.
            profile = algorithm.GetPreceding(profiles, transfers, out precedingStop, out transfers);
            Assert.AreEqual(0, transfers);
            Assert.AreEqual(0, precedingStop);
            profiles = algorithm.GetStopProfiles(precedingStop);
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 00 * 60, tripStatus.DepartureTime);

            tripStatus = algorithm.GetTripStatus(1);
            Assert.AreEqual(4, tripStatus.Transfers);
            Assert.AreEqual(1, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 15 * 60, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests a successful two-hop, one transfer versus a one-hop connection without transfers with a three-connection db.
        /// 
        /// Departure (0)@07:30:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @08:00          @08:10      
        /// 
        ///                   (1)-->---1--->--(2)
        ///                 @08:15          @08:25   
        ///   (0)------2------->-------2------(2)
        /// @08:16                          @08:25
        /// 
        /// </summary>
        [Test]
        public void TestTwoHopsOneTransferVersusOneHopSuccessful()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddStop(2, 2, 2);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.AddConnection(1, 2, 1, 8 * 3600 + 15 * 60, 8 * 3600 + 25 * 60);
            db.AddConnection(0, 2, 2, 8 * 3600 + 16 * 60, 8 * 3600 + 25 * 60);

            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            var algorithm = new ProfileSearch(db, departureTime,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(2, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(2, arrivalStops[2]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 25 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(55 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 25, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var precedingStop = Itinero.Transit.Constants.NoStopId;
            var transfers = Itinero.Transit.Constants.NoTransfers;

            // get profiles at stop 2.
            var profiles = algorithm.GetStopProfiles(2);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(2, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 55 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.DepartureStop);
            Assert.AreEqual(2, connections.TripId);

            // get previous profile and check this is stop 0.
            profile = algorithm.GetPreceding(profiles, 2, out precedingStop, out transfers);
            profiles = algorithm.GetStopProfiles(0);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            // check the profiles at stop 1.
            profiles = algorithm.GetStopProfiles(1);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.DepartureStop);
            Assert.AreEqual(0, connections.TripId);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 00 * 60, tripStatus.DepartureTime);

            tripStatus = algorithm.GetTripStatus(1);
            Assert.AreEqual(4, tripStatus.Transfers);
            Assert.AreEqual(1, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 15 * 60, tripStatus.DepartureTime);

            tripStatus = algorithm.GetTripStatus(2);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 16 * 60, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests a successful two-hop, one transfer with a two-connection db.
        /// 
        /// Departure (0)@07:30:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @08:00          @08:10      
        ///                     \ (-> transfer: 100 sec)
        ///                     (2)-->---1--->--(3)
        ///                   @08:15          @08:25  
        /// </summary>
        [Test]
        public void TestTwoHopsOneTransferCloseStopsSuccessful()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddStop(2, 2, 2);
            db.AddStop(3, 3, 3);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.AddConnection(2, 3, 1, 8 * 3600 + 15 * 60, 8 * 3600 + 25 * 60);

            db.SortConnections(DefaultSorting.DepartureTime, null);

            // build dummy transfers db.
            var transfersDb = new TransfersDb(1024);
            transfersDb.AddTransfer(1, 2, 100);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            var algorithm = new ProfileSearch(db, departureTime, transfersDb,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(3, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(5, arrivalStops.Count);
            Assert.AreEqual(3, arrivalStops[4]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(5, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 25 * 60, arrivalProfiles[4].Seconds);
            Assert.AreEqual(55 * 60, algorithm.Duration(4));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 25, 00), algorithm.ArrivalTime(4));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var profiles = algorithm.GetStopProfiles(3);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[3];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[4];
            Assert.AreEqual(1, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 55 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(2);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[3];
            Assert.IsTrue(profile.IsTransfer);
            Assert.AreEqual(1, profile.PreviousStopId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60 + 100, profile.Seconds);

            profiles = algorithm.GetStopProfiles(1);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(0);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 00 * 60, tripStatus.DepartureTime);

            tripStatus = algorithm.GetTripStatus(1);
            Assert.AreEqual(4, tripStatus.Transfers);
            Assert.AreEqual(2, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 15 * 60, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests a successful two-hop, one transfer with a three-connection db where one transfer connection is skipped.
        /// 
        /// Departure (0)@07:30:00
        /// 
        ///   (0)-->---0--->--(1)-->---0--->--(2)-->---0--->--(3)
        /// @08:00          @08:10          @08:15          @08:25  
        ///                     \             /   
        ///                      ------------
        ///       (transfer time smaller/bigger than 5 mins total)
        /// </summary>
        [Test]
        public void TestTwoHopsOneTransferCloseStopsSuccessfulSkippedPseudo()
        {
            // build dummy db.
            var db = new TransitDb();
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddStop(2, 2, 2);
            db.AddStop(3, 3, 3);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddTrip(0, 0, 0);
            db.AddConnection(0, 1, 0, 8 * 3600, 8 * 3600 + 10 * 60);
            db.AddConnection(1, 2, 0, 8 * 3600 + 10 * 60, 8 * 3600 + 15 * 60);
            db.AddConnection(2, 3, 0, 8 * 3600 + 15 * 60, 8 * 3600 + 25 * 60);

            db.SortConnections(DefaultSorting.DepartureTime, null);

            // build dummy transfers db.
            var transfersDb = new TransfersDb(1024);
            transfersDb.AddTransfer(1, 2, 60); // this leads to a transfer time faster than the actual connection.

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            var algorithm = new ProfileSearch(db, departureTime, transfersDb,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(3, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(3, arrivalStops[2]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 25 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(55 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 25, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var profiles = algorithm.GetStopProfiles(3);
            Assert.AreEqual(3, profiles.Count);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(2, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 55 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(2);
            Assert.AreEqual(4, profiles.Count);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(1, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 45 * 60, profile.Seconds);
            profile = profiles[3];
            Assert.IsTrue(profile.IsTransfer);
            Assert.AreEqual(1, profile.PreviousStopId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60 + 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(1);
            Assert.AreEqual(3, profiles.Count);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(0);
            Assert.AreEqual(1, profiles.Count);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            // build dummy transfers db.
            transfersDb = new TransfersDb(1024);
            transfersDb.AddTransfer(1, 2, 6 * 60); // this leads to a transfer time slower than the actual connection.

            // run algorithm.
            departureTime = new DateTime(2017, 05, 10, 07, 30, 00);
            algorithm = new ProfileSearch(db, departureTime, transfersDb,
                (profileId, day) => true);
            algorithm.SetSourceStop(0, 07 * 3600 + 30 * 60);
            algorithm.SetTargetStop(3, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(3, arrivalStops[2]);
            arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(08 * 3600 + 25 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(55 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 10, 08, 25, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            profiles = algorithm.GetStopProfiles(3);
            Assert.AreEqual(3, profiles.Count);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(2, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 55 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(2);
            Assert.AreEqual(3, profiles.Count);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(1, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 45 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(1);
            Assert.AreEqual(3, profiles.Count);
            profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 40 * 60, profile.Seconds);

            profiles = algorithm.GetStopProfiles(0);
            Assert.AreEqual(1, profiles.Count);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(08 * 3600 + 00 * 60, tripStatus.DepartureTime);
        }

        /// <summary>
        /// Tests a  one-hop with a one-connection db and with only monday as a possible connection after 01-01-2017.
        /// 
        /// Departure (0)@00:50:00
        /// 
        ///   (0)-->---0--->--(1)
        /// @01:00          @01:40
        /// 
        /// </summary>
        [Test]
        public void TestOneHopScheduled()
        {
            // build dummy db.
            var db = new TransitDb();
            var schedule = db.AddSchedule();
            db.AddScheduleEntry(schedule, new DateTime(2017, 01, 01), new DateTime(2018, 01, 01),
                DayOfWeek.Monday);
            db.AddStop(0, 0, 0);
            db.AddStop(1, 1, 1);
            db.AddTrip(schedule, 0, 0);
            db.AddConnection(0, 1, 0, 3600, 3600 + 40 * 60);
            db.SortConnections(DefaultSorting.DepartureTime, null);

            // run algorithm.
            var departureTime = new DateTime(2017, 05, 08, 00, 50, 00);
            var algorithm = new ProfileSearch(db, departureTime,
                db.GetIsTripPossibleFunc());
            algorithm.SetSourceStop(0, 50 * 60);
            algorithm.SetTargetStop(1, 0);
            algorithm.Run();

            // test results.
            Assert.IsTrue(algorithm.HasRun);
            Assert.IsTrue(algorithm.HasSucceeded);

            // check arrival profile(s).
            var arrivalStops = algorithm.ArrivalStops;
            Assert.AreEqual(3, arrivalStops.Count);
            Assert.AreEqual(1, arrivalStops[2]);
            var arrivalProfiles = algorithm.ArrivalProfiles;
            Assert.AreEqual(3, arrivalProfiles.Count);
            Assert.AreEqual(3600 * 01 + 40 * 60, arrivalProfiles[2].Seconds);
            Assert.AreEqual(50 * 60, algorithm.Duration(2));
            Assert.AreEqual(new DateTime(2017, 05, 08, 01, 40, 00), algorithm.ArrivalTime(2));

            // check stop profiles.
            var connections = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            var profiles = algorithm.GetStopProfiles(1);
            var profile = profiles[0];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[1];
            Assert.IsTrue(profile.IsEmpty);
            profile = profiles[2];
            Assert.AreEqual(0, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds + 50 * 60, profile.Seconds);
            connections.MoveTo(profile.PreviousConnectionId);
            Assert.AreEqual(0, connections.TripId);
            Assert.AreEqual(0, connections.DepartureStop);
            profiles = algorithm.GetStopProfiles(0);
            profile = profiles[0];
            Assert.AreEqual(Itinero.Transit.Constants.NoConnectionId, profile.PreviousConnectionId);
            Assert.AreEqual((int)(departureTime - departureTime.Date).TotalSeconds, profile.Seconds);

            var tripStatus = algorithm.GetTripStatus(0);
            Assert.AreEqual(2, tripStatus.Transfers);
            Assert.AreEqual(0, tripStatus.StopId);
            Assert.AreEqual(3600, tripStatus.DepartureTime);

            // run algorithm.
            departureTime = new DateTime(2017, 05, 10, 00, 50, 00);
            algorithm = new ProfileSearch(db, departureTime,
                db.GetIsTripPossibleFunc());
            algorithm.SetSourceStop(0, 50 * 60);
            algorithm.SetTargetStop(1, 0);
            algorithm.Run();

            Assert.IsTrue(algorithm.HasRun);
            Assert.IsFalse(algorithm.HasSucceeded);
        }
    }
}