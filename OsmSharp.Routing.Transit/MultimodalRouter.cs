﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2015 Abelshausen Ben
// 
// This file is part of OsmSharp.
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
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using OsmSharp.Routing.Profiles;
using OsmSharp.Routing.Transit.Algorithms;
using OsmSharp.Routing.Transit.Algorithms.OneToOne;
using OsmSharp.Routing.Transit.Data;
using System;

namespace OsmSharp.Routing.Transit
{
    /// <summary>
    /// An implementation of a multi modal router.
    /// </summary>
    public class MultimodalRouter : Router, IMultimodalRouter
    {
        private readonly TransitDb _db;
        private readonly Profile _transferProfile;

        /// <summary>
        /// Creates a multimodal router.
        /// </summary>
        public MultimodalRouter(RouterDb routerDb, TransitDb db, Profile transferProfile)
            : base(routerDb)
        {
            _db = db;
            _transferProfile = transferProfile;
        }

        /// <summary>
        /// Tries to calculate an earliest arrival route from stop1 to stop2.
        /// </summary>
        public Result<Route> TryEarliestArrival(DateTime departureTime,
            RouterPoint sourcePoint, Profile sourceProfile, RouterPoint targetPoint, Profile targetProfile, 
                EarliestArrivalSettings settings)
        {
            // create the profile search.
            var tripEnumerator = _db.GetTripsEnumerator();
            var transfersDb =  _db.GetTransfersDb(_transferProfile);
            var profileSearch = new ProfileSearch(_db, departureTime, transfersDb, (t, day) =>
            {
                if (tripEnumerator.MoveTo(t))
                {
                    if (settings.UseAgency == null ||
                        settings.UseAgency(tripEnumerator.AgencyId))
                    {
                        return true;
                    }
                }
                return false;
            });

            // search for sources.
            var departureTimeSeconds = (uint)(departureTime - departureTime.Date).TotalSeconds;
            var sourceSearch = new ClosestStopsSearch(this.Db, _db, sourceProfile, sourcePoint,
                settings.MaxSecondsSource, false);
            sourceSearch.StopFound = (s, t) =>
                {
                    profileSearch.SetSourceStop(s, departureTimeSeconds + (uint)t);
                    return false;
                };
            sourceSearch.Run();
            if(!sourceSearch.HasRun ||
               !sourceSearch.HasSucceeded)
            {
                return new Result<Route>("Searching for source stops failed.");
            }

            // search for targets.
            var targetSearch = new ClosestStopsSearch(this.Db, _db, targetProfile, targetPoint,
                settings.MaxSecondsTarget, true);
            targetSearch.StopFound = (s, t) =>
            {
                profileSearch.SetTargetStop(s, (uint)t);
                return false;
            };
            targetSearch.Run();
            if (!targetSearch.HasRun ||
                !targetSearch.HasSucceeded)
            {
                return new Result<Route>("Searching for target stops failed.");
            }

            // run actual profile search.
            profileSearch.Run();
            if (!profileSearch.HasRun ||
                !profileSearch.HasSucceeded)
            {
                return new Result<Route>("No route found.");
            }

            // build routes.
            var profileSearchRouteBuilder = new ProfileSearchRouteBuilder(profileSearch);
            profileSearchRouteBuilder.Run();
            if (!profileSearchRouteBuilder.HasRun ||
                !profileSearchRouteBuilder.HasSucceeded)
            {
                return new Result<Route>(string.Format("Route could not be built: {0}.", profileSearchRouteBuilder.ErrorMessage));
            }

            // build source route.
            var sourceRoute = sourceSearch.GetRoute(profileSearchRouteBuilder.Stops[0]);

            // build target route.
            var targetRoute = targetSearch.GetRoute(profileSearchRouteBuilder.Stops[profileSearchRouteBuilder.Stops.Count - 1]);

            var route = sourceRoute.Concatenate(profileSearchRouteBuilder.Route);
            route = route.Concatenate(targetRoute);

            return new Result<Route>(route);
        }
    }
}