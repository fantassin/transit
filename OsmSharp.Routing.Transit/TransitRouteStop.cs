﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2014 Abelshausen Ben
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

using GTFS.Entities;
using System;

namespace OsmSharp.Routing.Transit
{
    /// <summary>
    /// Represents a transit route stop containing a stop and a time.
    /// </summary>
    public class TransitRouteStop
    {
        /// <summary>
        /// Gets or sets the stop.
        /// </summary>
        public Stop Stop { get; set; }

        /// <summary>
        /// Gets or sets the time.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Returns a description of this stop.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}@{1}", this.Stop, this.Time);
        }
    }
}