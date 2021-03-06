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

using Itinero.Algorithms;
using Itinero.Algorithms.Default;
using Itinero.Algorithms.Routes;
using Itinero.LocalGeo;
using Itinero.Graphs;
using Itinero.Navigation.Directions;
using Itinero.Profiles;
using Itinero.Transit.Algorithms.Search;
using Itinero.Transit.Data;
using System;
using System.Threading;
using Itinero.Algorithms.Weights;
using Itinero.Data.Network;

namespace Itinero.Transit.Algorithms
{
    /// <summary>
    /// Closest stop search.
    /// </summary>
    public class ClosestStopsSearch : AlgorithmBase
    {
        private readonly MultimodalDb _multimodalDb;
        private readonly Func<ushort, Factor> _getFactor;
        private readonly Profile _profile;
        private readonly RouterPoint _routerPoint;
        private readonly float _maxSeconds;
        private readonly bool _backward;

        /// <summary>
        /// Creates a new closest stop search.
        /// </summary>
        public ClosestStopsSearch(MultimodalDb multimodalDb, Profile profile,
            RouterPoint routerPoint, float maxSeconds, bool backward)
            : this(multimodalDb, profile, (p) => profile.Factor(multimodalDb.RouterDb.EdgeProfiles.Get(p)), routerPoint,
                  maxSeconds, backward)
        {

        }

        /// <summary>
        /// Creates a new closest stop search.
        /// </summary>
        public ClosestStopsSearch(MultimodalDb multimodalDb, Profile profile, Func<ushort, Factor> getFactor,
            RouterPoint routerPoint, float maxSeconds, bool backward)
        {
            if (profile.Metric != ProfileMetric.TimeInSeconds)
            { // oeps, this profile is not time-based!
                throw new ArgumentOutOfRangeException(
                    "The default closest stop search can only be used with profiles that use time in seconds as a metric.");
            }

            _multimodalDb = multimodalDb;
            _profile = profile;
            _getFactor = getFactor;
            _routerPoint = routerPoint;
            _maxSeconds = maxSeconds;
            _backward = backward;

            // build search.
            _dykstra = new Dykstra(_multimodalDb.RouterDb.Network.GeometricGraph.Graph, new DefaultWeightHandler(_getFactor), x => Itinero.Constants.NO_VERTEX, 
                _routerPoint.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), !_backward), _maxSeconds, _backward);
        }

        private System.Collections.Generic.Dictionary<uint, LinkedStopRouterPoint> _stopsPerEdge;
        private Dykstra _dykstra = null;
        private StopLinksDb.Enumerator _stopLinksDbEnumerator;

        /// <summary>
        /// Executes the actual run.
        /// </summary>
        protected override void DoRun(CancellationToken cancellationToken)
        {
            // get the stop links db for the profile given.
            var stopLinksDb = _multimodalDb.GetStopLinksDb(_profile);
            _stopLinksDbEnumerator = stopLinksDb.GetEnumerator();

            // find the closest stops within the range of the 'maxSeconds' parameter.
            var distance = ((3 / 3.6f) / 1.0f) * _maxSeconds;
            var stopsDbEnumerator = _multimodalDb.TransitDb.GetStopsEnumerator();
            var location = new Coordinate(_routerPoint.Latitude, _routerPoint.Longitude);
            var box = new Box(
                location.OffsetWithDirection(distance, DirectionEnum.West)
                    .OffsetWithDirection(distance, DirectionEnum.North),
                location.OffsetWithDirection(distance, DirectionEnum.East)
                    .OffsetWithDirection(distance, DirectionEnum.South));
            var closestStops = stopsDbEnumerator.Search(box.MinLat, box.MinLon, 
                box.MaxLat, box.MaxLon);
            _stopsPerEdge = new System.Collections.Generic.Dictionary<uint, LinkedStopRouterPoint>();
            foreach(var stop in closestStops)
            {
                _stopLinksDbEnumerator.MoveTo(stop);

                while (_stopLinksDbEnumerator.MoveNext())
                {
                    var newStopRouterPoint = new LinkedStopRouterPoint()
                        {
                            RouterPoint = new RouterPoint(0, 0, _stopLinksDbEnumerator.EdgeId, _stopLinksDbEnumerator.Offset),
                            StopId = stop
                        };
                    LinkedStopRouterPoint stopRouterPoint;
                    if (_stopsPerEdge.TryGetValue(_stopLinksDbEnumerator.EdgeId, out stopRouterPoint))
                    {
                        stopRouterPoint.Next = newStopRouterPoint;
                    }
                    else
                    {
                        _stopsPerEdge[_stopLinksDbEnumerator.EdgeId] = newStopRouterPoint;
                    }
                }
            }

            // report on source edges and build source paths.
            var paths = _routerPoint.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), !_backward);
            for (var p = 0; p < paths.Length; p++)
            {
                var edgeId = _routerPoint.EdgeId;
                LinkedStopRouterPoint stopRouterPoint;
                if (_stopsPerEdge.TryGetValue(edgeId, out stopRouterPoint))
                {
                    if (_routerPoint.EdgeId != edgeId)
                    { // ok, this is not the source-ege.
                        throw new Exception("Cannot report on an edge that is not the source when not searching yet.");
                    }

                    // move the stop links enumerator.
                    while (stopRouterPoint != null)
                    {
                        var stopId = stopRouterPoint.StopId;
                        var routerPoint = stopRouterPoint.RouterPoint;

                        if (!_backward)
                        { // forward, route from source to stop.
                            var path = _routerPoint.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), routerPoint);
                            if (path != null)
                            { // there is a path, report on it.
                                if (this.StopFound(stopId, path.Weight))
                                { // report this edge was found.
                                    this.HasSucceeded = true;
                                    return;
                                }
                            }
                        }
                        else
                        { // backward, route from stop to source.
                            var path = routerPoint.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _routerPoint);
                            if (path != null)
                            { // there is a path, report on it.
                                if (this.StopFound(stopId, path.Weight))
                                { // report this edge was found.
                                    this.HasSucceeded = true;
                                    return;
                                }
                            }
                        }

                        stopRouterPoint = stopRouterPoint.Next;
                    }
                }
            }

            // execute dykstra search from all sources.
            _dykstra.WasFound = this.WasEdgeFoundInternal;
            //_dykstra.WasFound = this.WasFoundInternal;
            _dykstra.Run();

            this.HasSucceeded = true;
        }

        /// <summary>
        /// A function to report that a stop was found and the number of seconds to travel to/from.
        /// </summary>
        public delegate bool StopFoundFunc(uint stop, float time);

        /// <summary>
        /// Gets or sets the stop found function.
        /// </summary>
        public virtual StopFoundFunc StopFound
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the dykstra search.
        /// </summary>
        public Dykstra Search
        {
            get
            {
                return _dykstra;
            }
        }

        /// <summary>
        /// Gets or sets the wasfound function to be called when a new vertex is found.
        /// </summary>
        public Dykstra.WasFoundDelegate WasEdgeFound { get; set; }

        /// <summary>
        /// Called when a new edge was found.
        /// </summary>
        private bool WasEdgeFoundInternal(uint vertex, float weight)
        {
            if (this.WasEdgeFound != null &&
               this.WasEdgeFound(vertex, weight))
            { // somewhere else the descision was made to stop the search here.
                return true;
            }

            EdgePath<float> visit;
            if (!_dykstra.TryGetVisit(vertex, out visit))
            { // could not get visit, can't act on it.
                return false;
            }
            if (visit.Edge == Itinero.Constants.NO_EDGE)
            { // no edge information.
                return false;
            }

            uint edgeId = Itinero.Constants.NO_EDGE;
            if (visit.Edge > 0)
            {
                edgeId = (uint)(visit.Edge - 1);
            }
            else
            {
                edgeId = (uint)(-visit.Edge - 1);
            }

            var edge = _multimodalDb.RouterDb.Network.GetEdge(visit.Edge);
            var vertex1 = edge.From;
            if (visit.Edge < 0)
            {
                vertex1 = edge.To;
            }

            LinkedStopRouterPoint stopRouterPoint;
            if(_stopsPerEdge.TryGetValue(edgeId, out stopRouterPoint))
            { // ok, there is a link, get the router points and calculate weights.
                if (_dykstra == null)
                { // no dykstra search just yet, this is the source-edge.
                    throw new Exception("Could not get visit of other vertex for settled edge.");
                }
                EdgePath<float> vertex1Visit;
                if (!_dykstra.TryGetVisit(vertex1, out vertex1Visit))
                {
                    throw new Exception("Could not get visit of other vertex for settled edge.");
                }

                // move the stop links enumerator.
                while (stopRouterPoint != null)
                {
                    var stopId = stopRouterPoint.StopId;
                    var routerPoint = stopRouterPoint.RouterPoint;

                    var paths = routerPoint.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _backward);
                    if (paths[0].Vertex == vertex1)
                    { // report on the time.
                        if (this.StopFound(stopId, vertex1Visit.Weight + paths[0].Weight))
                        {
                            return true;
                        }
                    }
                    else if (paths.Length > 1 && paths[1].Vertex == vertex1)
                    { // report on the time.
                        if (this.StopFound(stopId, vertex1Visit.Weight + paths[1].Weight))
                        {
                            return true;
                        }
                    }

                    stopRouterPoint = stopRouterPoint.Next;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the weight to the given stop.
        /// </summary>
        public float GetWeight(uint stop)
        {
            var bestWeight = float.MaxValue;
            _stopLinksDbEnumerator.MoveTo(stop);
            while (_stopLinksDbEnumerator.MoveNext())
            {
                var point = new RouterPoint(0, 0, _stopLinksDbEnumerator.EdgeId,
                    _stopLinksDbEnumerator.Offset);
                if (point.EdgeId == _routerPoint.EdgeId)
                { // on the same edge.
                    EdgePath<float> path;
                    if (_backward)
                    { // from stop -> source.
                        path = point.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _routerPoint);
                    }
                    else
                    { // from source -> stop.
                        path = _routerPoint.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), point);
                    }
                    if (path.Weight < bestWeight)
                    { // set as best because improvement.
                        bestWeight = path.Weight;
                    }
                }
                else
                { // on different edge, to the usual.
                    var paths = point.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_profile.GetGetFactor(_multimodalDb.RouterDb)), _backward);
                    EdgePath<float> visit;
                    if (_dykstra.TryGetVisit(paths[0].Vertex, out visit))
                    { // check if this one is better.
                        if (visit.Weight + paths[0].Weight < bestWeight)
                        { // concatenate paths and set best.
                            EdgePath<float> best;
                            if (paths[0].Weight == 0)
                            { // just use the visit.
                                best = visit;
                            }
                            else
                            { // there is a distance/weight.
                                best = new EdgePath<float>(Itinero.Constants.NO_VERTEX,
                                    paths[0].Weight + visit.Weight, visit);
                            }
                            bestWeight = best.Weight;
                        }
                    }
                    if (paths.Length > 1 && _dykstra.TryGetVisit(paths[1].Vertex, out visit))
                    { // check if this one is better.
                        if (visit.Weight + paths[1].Weight < bestWeight)
                        { // concatenate paths and set best.
                            EdgePath<float> best;
                            if (paths[1].Weight == 0)
                            { // just use the visit.
                                best = visit;
                            }
                            else
                            { // there is a distance/weight.
                                best = new EdgePath<float>(Itinero.Constants.NO_VERTEX,
                                    paths[1].Weight + visit.Weight, visit);
                            }
                            bestWeight = best.Weight;
                        }
                    }
                }
            }
            return bestWeight;
        }

        /// <summary>
        /// Gets the path to the given stop.
        /// </summary>
        public EdgePath<float> GetPath(uint stop)
        {
            EdgePath<float> best = null;
            var bestWeight = float.MaxValue;
            _stopLinksDbEnumerator.MoveTo(stop);
            while (_stopLinksDbEnumerator.MoveNext())
            {
                var point = new RouterPoint(0, 0, _stopLinksDbEnumerator.EdgeId, 
                    _stopLinksDbEnumerator.Offset);
                if (point.EdgeId == _routerPoint.EdgeId)
                { // on the same edge.
                    EdgePath<float> path;
                    if (_backward)
                    { // from stop -> source.
                        path = point.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _routerPoint);
                    }
                    else
                    { // from source -> stop.
                        path = _routerPoint.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), point);
                    }
                    if (path.Weight < bestWeight)
                    { // set as best because improvement.
                        best = path;
                        bestWeight = path.Weight;
                    }
                }
                else
                { // on different edge, to the usual.
                    var paths = point.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _backward);
                    EdgePath<float> visit;
                    if (_dykstra.TryGetVisit(paths[0].Vertex, out visit))
                    { // check if this one is better.
                        if (visit.Weight + paths[0].Weight < bestWeight)
                        { // concatenate paths and set best.
                            if (paths[0].Weight == 0)
                            { // just use the visit.
                                best = visit;
                            }
                            else
                            { // there is a distance/weight.
                                best = new EdgePath<float>(Itinero.Constants.NO_VERTEX,
                                    paths[0].Weight + visit.Weight, visit);
                            }
                            bestWeight = best.Weight;
                        }
                    }
                    if (paths.Length > 1 && _dykstra.TryGetVisit(paths[1].Vertex, out visit))
                    { // check if this one is better.
                        if (visit.Weight + paths[1].Weight < bestWeight)
                        { // concatenate paths and set best.
                            if (paths[1].Weight == 0)
                            { // just use the visit.
                                best = visit;
                            }
                            else
                            { // there is a distance/weight.
                                best = new EdgePath<float>(Itinero.Constants.NO_VERTEX,
                                    paths[1].Weight + visit.Weight, visit);
                            }
                            bestWeight = best.Weight;
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Gets the target router point for the given stop.
        /// </summary>
        public RouterPoint GetTargetPoint(uint stop)
        {
            RouterPoint best = null;
            var bestWeight = float.MaxValue;
            var stopEnumerator = _multimodalDb.TransitDb.GetStopsEnumerator();
            if (stopEnumerator.MoveTo(stop))
            {
                _stopLinksDbEnumerator.MoveTo(stop);
                while (_stopLinksDbEnumerator.MoveNext())
                {
                    var point = new RouterPoint(stopEnumerator.Latitude, stopEnumerator.Longitude, _stopLinksDbEnumerator.EdgeId,
                        _stopLinksDbEnumerator.Offset);
                    if (point.EdgeId == _routerPoint.EdgeId)
                    { // on the same edge.
                        EdgePath<float> path;
                        if (_backward)
                        { // from stop -> source.
                            path = point.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _routerPoint);
                        }
                        else
                        { // from source -> stop.
                            path = _routerPoint.EdgePathTo(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), point);
                        }
                        if (path.Weight < bestWeight)
                        { // set as best because improvement.
                            best = point;
                            bestWeight = path.Weight;
                        }
                    }
                    else
                    { // on different edge, to the usual.
                        var paths = point.ToEdgePaths(_multimodalDb.RouterDb, new DefaultWeightHandler(_getFactor), _backward);
                        EdgePath<float> visit;
                        if (_dykstra.TryGetVisit(paths[0].Vertex, out visit))
                        { // check if this one is better.
                            if (visit.Weight + paths[0].Weight < bestWeight)
                            { // concatenate paths and set best.
                                if (paths[0].Weight == 0)
                                { // just use the visit.
                                    best = point;
                                    bestWeight = visit.Weight;
                                }
                                else
                                { // there is a distance/weight.
                                    best = point;
                                    bestWeight = paths[0].Weight + visit.Weight;
                                }
                            }
                        }
                        if (paths.Length > 1 && _dykstra.TryGetVisit(paths[1].Vertex, out visit))
                        { // check if this one is better.
                            if (visit.Weight + paths[1].Weight < bestWeight)
                            { // concatenate paths and set best.
                                if (paths[1].Weight == 0)
                                { // just use the visit.
                                    best = point;
                                    bestWeight = visit.Weight;
                                }
                                else
                                { // there is a distance/weight.
                                    best = point;
                                    bestWeight = paths[1].Weight + visit.Weight;
                                }
                            }
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Gets the route to the given stop.
        /// </summary>
        public Route GetRoute(uint stop)
        {
            var path = this.GetPath(stop);
            var point = this.GetTargetPoint(stop);

            if (_backward)
            {
                var reverse = new EdgePath<float>(path.Vertex);
                path = reverse.Append(path);
                return CompleteRouteBuilder.Build(_multimodalDb.RouterDb, _profile, point, _routerPoint, path);
            }
            return CompleteRouteBuilder.Build(_multimodalDb.RouterDb, _profile, _routerPoint, point, path);
        }

        /// <summary>
        /// A linked stop router point.
        /// </summary>
        private class LinkedStopRouterPoint
        {
            /// <summary>
            /// Gets or sets the router point.
            /// </summary>
            public RouterPoint RouterPoint { get; set; }

            /// <summary>
            /// Gets or sets the stop id.
            /// </summary>
            public uint StopId { get; set; }

            /// <summary>
            /// Gets or sets the next link.
            /// </summary>
            public LinkedStopRouterPoint Next { get; set; }
        }
    }
}