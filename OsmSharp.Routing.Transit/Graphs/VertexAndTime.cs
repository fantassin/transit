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

namespace OsmSharp.Routing.Transit.Graphs
{
    /// <summary>
    /// Represents a vertex and a point in time since the start of the route.
    /// </summary>
    public struct VertexTimeAndTrip
    {
        /// <summary>
        /// Creates a vertex that exists in all times and for all trips.
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="secondsMode"></param>
        public VertexTimeAndTrip(long vertex)
            : this(vertex, 0)
        {
            
        }

        /// <summary>
        /// Creates a vertex that exists in all times and for all trips.
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="secondsMode"></param>
        public VertexTimeAndTrip(long vertex, uint secondsMode)
            : this()
        {
            this.Vertex = vertex;
            this.Trip = 0;
            this.Seconds = 0;
            this.SecondsMode = secondsMode;
        }

        /// <summary>
        /// Creates an vertex and time.
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="seconds"></param>
        /// <param name="trip"></param>
        public VertexTimeAndTrip(long vertex, uint seconds, uint trip)
            : this()
        {
            this.Vertex = vertex;
            this.Seconds = seconds;
            this.Trip = trip;
            this.SecondsMode = 0;
        }

        /// <summary>
        /// Gets or sets the vertex.
        /// </summary>
        public long Vertex { get; set; }

        /// <summary>
        /// Gets or sets the seconds from the start.
        /// </summary>
        public uint Seconds { get; set; }

        /// <summary>
        /// Gets or sets the seconds from the last mode change.
        /// </summary>
        public uint SecondsMode { get; set; }

        /// <summary>
        /// Gets or sets the trip.
        /// </summary>
        public uint Trip { get; set; }

        /// <summary>
        /// Returns a representation of this vertex in text.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}@{1} {2}",
                this.Vertex, this.Trip, this.Seconds);
        }

        /// <summary>
        /// Returns the hashcode for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.Vertex.GetHashCode() ^
                this.Seconds.GetHashCode() ^
                this.Trip.GetHashCode();
        }

        /// <summary>
        /// Returns true if the given object represents the same information.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is VertexTimeAndTrip)
            {
                var other = (VertexTimeAndTrip)obj;
                return other.Seconds == this.Seconds &&
                    other.Vertex == this.Vertex &&
                    other.Trip.Equals(this.Trip);
            }
            return false;
        }
    }
}