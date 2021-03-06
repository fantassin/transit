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

using Itinero.Attributes;
using Itinero.LocalGeo;
using Itinero.Profiles;
using Reminiscence.Arrays;
using System;
using System.Collections.Generic;

namespace Itinero.Transit.Data
{
    /// <summary>
    /// Contains extension methods for the transit db.
    /// </summary>
    public static class TransitDbExtensions
    {
        /// <summary>
        /// Adds a transfers db.
        /// </summary>
        public static void AddTransfersDb(this TransitDb db, Profiles.Profile profile, IAttributeCollection defaultProfile,
            float maxTimeInSeconds)
        {
            var transfersDb = new TransfersDb(db.StopsCount);
            var factor = profile.Factor(defaultProfile);

            // add all transfers.
            var enumerator1 = db.GetStopsEnumerator();
            while (enumerator1.MoveNext())
            {
                var enumerator2 = db.GetStopsEnumerator();
                while (enumerator2.MoveNext())
                {
                    if (enumerator1.Id < enumerator2.Id)
                    {
                        var distance = Coordinate.DistanceEstimateInMeter(enumerator1.Latitude, enumerator1.Longitude,
                            enumerator2.Latitude, enumerator2.Longitude);
                        var time = (int)System.Math.Round(distance * factor.Value, 0);
                        if (time < maxTimeInSeconds)
                        {
                            transfersDb.AddTransfer(enumerator1.Id, enumerator2.Id, time);
                        }
                    }
                }
            }

            db.AddTransfersDb(profile, transfersDb);
        }

        /// <summary>
        /// Adds a new schedule entry.
        /// </summary>
        public static void AddScheduleEntry(this TransitDb db, uint id, DateTime day)
        {
            db.AddScheduleEntry(id, day, day, day.Weekmask());
        }

        /// <summary>
        /// Adds a new schedule entry.
        /// </summary>
        public static void AddScheduleEntry(this TransitDb db, uint id, DateTime start, DateTime end,
            params DayOfWeek[] days)
        {
            if (days == null || days.Length == 0) { throw new ArgumentOutOfRangeException("days", "Cannot add empty week patterns."); }

            db.AddScheduleEntry(id, start, end, SchedulesDbExtensions.Weekmask(days));
        }

        /// <summary>
        /// Searches for the first stop with some tags or based on some condition.
        /// </summary>
        public static uint SearchFirstStopsWithTags(this TransitDb db,
            Func<IAttributeCollection, bool> condition)
        {
            var stops = new HashSet<uint>();
            var enumerator = db.GetStopsEnumerator();
            enumerator.Reset();
            while (enumerator.MoveNext())
            {
                var stopTags = db.StopAttributes.Get(enumerator.MetaId);
                if (condition(stopTags))
                {
                    return enumerator.Id;
                }
            }
            return Constants.NoStopId;
        }

        /// <summary>
        /// Searches the stops with some tags or based on some condition.
        /// </summary>
        public static HashSet<uint> SearchStopsWithTags(this TransitDb db,
            Func<IAttributeCollection, bool> condition)
        {
            var stops = new HashSet<uint>();
            var enumerator = db.GetStopsEnumerator();
            enumerator.Reset();
            while (enumerator.MoveNext())
            {
                var stopTags = db.StopAttributes.Get(enumerator.MetaId);
                if(condition(stopTags))
                {
                    stops.Add(enumerator.Id);
                }
            }
            return stops;
        }

        /// <summary>
        /// Gets the default is trip possible function.
        /// </summary>
        public static Func<uint, DateTime, bool> GetIsTripPossibleFunc(this TransitDb db)
        {
            var tripEnumerator = db.GetTripsEnumerator();
            var schedulesSnumerator = db.GetSchedulesEnumerator();

            return (tripId, day) =>
                {
                    if(tripEnumerator.MoveTo(tripId))
                    {
                        if(schedulesSnumerator.MoveTo(tripEnumerator.ScheduleId))
                        {
                            return schedulesSnumerator.DateIsSet(day);
                        }
                    }
                    return false;
                };
        }

        /// <summary>
        /// Gets the meta-data for the given stop.
        /// </summary>
        public static IAttributeCollection GetStopMeta(this TransitDb db, uint stopId)
        {
            var enumerator = db.GetStopsEnumerator();
            if(!enumerator.MoveTo(stopId))
            {
                return null;
            }
            return db.StopAttributes.Get(enumerator.MetaId);
        }

        /// <summary>
        /// Copies all core data stops, schedules, trips, and connections from the given transit db.
        /// </summary>
        public static void CopyFrom(this TransitDb db, TransitDb other)
        {
            if (db == null) { throw new ArgumentNullException("db"); }
            if (other == null) { throw new ArgumentNullException("other"); }
            if (other.ConnectionSorting == null) { throw new ArgumentException("A database can only be copied if connections are sorted."); }
            
            var stopIdsBlockSize = 1024 * 32;
            var stopIds = new MemoryArray<uint>(stopIdsBlockSize);
            var tripIdsBlockSize = 1024 * 32;
            var tripIds = new MemoryArray<uint>(tripIdsBlockSize);
            var scheduleIds = new Dictionary<uint, uint>();
            var agencyIds = new Dictionary<uint, uint>();

            // copy stops and keep id transformations.
            var stopsEnumerator = other.GetStopsEnumerator();
            while(stopsEnumerator.MoveNext())
            {
                var stopsMeta = other.StopAttributes.Get(stopsEnumerator.MetaId);
                var newMetaId = db.StopAttributes.Add(stopsMeta);
                var newStopId = db.AddStop(stopsEnumerator.Latitude, stopsEnumerator.Longitude, newMetaId);
                if (stopsEnumerator.Id >= stopIds.Length)
                {
                    stopIds.Resize(stopIds.Length + stopIdsBlockSize);
                }
                stopIds[stopsEnumerator.Id] = newStopId;
            }

            // copy trips, copy schedules that have not been copied yet, and keep trip id transformations.
            var tripsEnumerator = other.GetTripsEnumerator();
            var scheduleEnumerator = other.GetSchedulesEnumerator();
            while (tripsEnumerator.MoveNext())
            {
                var tripsMeta = other.TripAttributes.Get(tripsEnumerator.MetaId);
                var newMetaId = db.TripAttributes.Add(tripsMeta);

                uint newAgencyMetaId = uint.MaxValue;
                if (!agencyIds.TryGetValue(tripsEnumerator.AgencyId, out newAgencyMetaId))
                {
                    var agencyMeta = other.AgencyAttributes.Get(tripsEnumerator.AgencyId);
                    newAgencyMetaId = db.AgencyAttributes.Add(agencyMeta);
                    agencyIds.Add(tripsEnumerator.AgencyId, newAgencyMetaId);
                }

                uint newScheduleId = uint.MaxValue;
                if(!scheduleIds.TryGetValue(tripsEnumerator.ScheduleId, out newScheduleId))
                {
                    if(scheduleEnumerator.MoveTo(tripsEnumerator.ScheduleId))
                    {
                        newScheduleId = scheduleEnumerator.CopyTo(db.SchedulesDb);
                        scheduleIds[tripsEnumerator.ScheduleId] = newScheduleId;
                    }
                }

                var newTripId = db.AddTrip(newScheduleId, newAgencyMetaId, newMetaId);
                if (tripsEnumerator.Id >= tripIds.Length)
                {
                    tripIds.Resize(tripIds.Length + tripIdsBlockSize);
                }
                tripIds[tripsEnumerator.Id] = newTripId;
            }

            // copy connections.
            var connectionEnumerator = other.GetConnectionsEnumerator(other.ConnectionSorting.Value);
            while(connectionEnumerator.MoveNext())
            {
                var newArrivalStop = stopIds[connectionEnumerator.ArrivalStop];
                var newDepartureStop = stopIds[connectionEnumerator.DepartureStop];
                var newTripId = tripIds[connectionEnumerator.TripId];

                db.AddConnection(newDepartureStop, newArrivalStop, newTripId, connectionEnumerator.DepartureTime, 
                    connectionEnumerator.ArrivalTime);
            }

            // copy shapes.
            if (other.ShapesDb != null)
            {
                var shapesEnumerator = other.ShapesDb.GetEnumerator();
                while (shapesEnumerator.MoveNext())
                {
                    var stop1 = stopIds[shapesEnumerator.Stop1];
                    var stop2 = stopIds[shapesEnumerator.Stop2];

                    db.ShapesDb.Add(stop1, stop2, shapesEnumerator.Shape);
                }
            }
        }

        /// <summary>
        /// Sorts the stops and updates the connections.
        /// </summary>
        public static void SortStops(this TransitDb db)
        {
            if(db.HasTransfersDb()) { throw new ArgumentException("Cannot sort stops for a transit db with tranfers db's. Add tranfer db's after sorting."); }

            // add default stop ids.
            var stopIds = new MemoryArray<uint>(db.StopsCount);
            for(uint s = 0; s < db.StopsCount; s++)
            {
                stopIds[s] = s;
            }

            // sort stops.
            db.SortStops((s1, s2) =>
            {
                var temp = stopIds[s1];
                stopIds[s1] = stopIds[s2];
                stopIds[s2] = temp;
            });

            var reverseStopIds = new MemoryArray<uint>(db.StopsCount);
            for(uint s = 0; s < db.StopsCount; s++)
            {
                reverseStopIds[stopIds[s]] = s;
            }
            stopIds = null;

            // update stops on connections.
            var connectionEnumerator = db.GetConnectionsEnumerator(DefaultSorting.DepartureTime);
            while (connectionEnumerator.MoveNext())
            {
                db.ConnectionsDb.UpdateStops(connectionEnumerator.Id,
                    reverseStopIds[connectionEnumerator.DepartureStop],
                    reverseStopIds[connectionEnumerator.ArrivalStop]);
            }

            // update stops on shapes.
            var shapesEnumerator = db.ShapesDb.GetEnumerator();
            while (shapesEnumerator.MoveNext())
            {
                db.ShapesDb.UpdateStops(shapesEnumerator,
                    reverseStopIds[shapesEnumerator.Stop1],
                    reverseStopIds[shapesEnumerator.Stop2]);
            }
            db.ShapesDb.Sort(); // sort again, stops have changed!
        }
    }
}