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

using Itinero.Profiles;
using Reminiscence.Arrays;
using System;
using System.IO;

namespace Itinero.Transit.Data
{
    /// <summary>
    /// A stop links db.
    /// </summary>
    public class StopLinksDb
    {
        private readonly ArrayBase<uint> _pointers; // holds the pointers/sizes.
        private readonly ArrayBase<uint> _data; // holds the actual links.
        private readonly Guid _id; // hold the network-id.
        private readonly string _profileName; // hold the routing profile the links are for.

        /// <summary>
        /// Creates a new stop links db.
        /// </summary>
        public StopLinksDb(uint size, RouterDb routerDb, Profile profile)
            : this(size, routerDb, profile.Name)
        {

        }

        /// <summary>
        /// Creates a new stop links db.
        /// </summary>
        public StopLinksDb(uint size, RouterDb routerDb, string profileName)
        {
            _pointers = new MemoryArray<uint>(size * 2);
            _data = new MemoryArray<uint>(size * 2);
            _profileName = profileName;

            _id = routerDb.Guid;
        }

        /// <summary>
        /// Creates a new stop links db.
        /// </summary>
        private StopLinksDb(Guid id, string profileName, ArrayBase<uint> pointers, ArrayBase<uint> data)
        {
            _id = id;
            _profileName = profileName;
            _pointers = pointers;
            _data = data;

            _nextPointer = (uint)pointers.Length;
        }

        private uint _nextPointer = 0;

        /// <summary>
        /// Gets the network id.
        /// </summary>
        public Guid Guid
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// Adds a new router point for the given stop.
        /// </summary>
        public void Add(uint stopId, RouterPoint point)
        {
            var pointerSize = _pointers.Length;
            var pointerStop = stopId * 2;
            while (pointerSize <= pointerStop)
            {
                pointerSize += 1024;
            }
            _pointers.Resize(pointerSize);

            // increase count or set pointer for the first time.
            if (_pointers[pointerStop + 0] == 0)
            { // set first pointer.
                _pointers[pointerStop + 0] = _nextPointer;
                _pointers[pointerStop + 1] = 1;
            }
            else if(_pointers[pointerStop + 0] + _pointers[pointerStop + 1] * 2 !=
                _nextPointer)
            { // invalid operation, can only add data to last added stop.
                throw new ArgumentException("Can only add stop links for the last added stop.");
            }
            else
            { // increase count.
                _pointers[pointerStop + 1] += 1;
            }

            // add data at the end.
            if(_nextPointer >= _data.Length)
            {
                _data.Resize(_data.Length + 1024);
            }
            _data[_nextPointer + 0] = point.EdgeId;
            _data[_nextPointer + 1] = point.Offset;
            _nextPointer += 2;
        }

        /// <summary>
        /// Gets a new enumerator.
        /// </summary>
        /// <returns></returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Gets the profile name.
        /// </summary>
        public string ProfileName
        {
            get
            {
                return _profileName;
            }
        }

        /// <summary>
        /// An enumerator.
        /// </summary>
        public class Enumerator
        {
            private readonly StopLinksDb _db;

            internal Enumerator(StopLinksDb db)
            {
                _db = db;
            }

            private uint _count = uint.MaxValue;
            private uint _pointer = uint.MaxValue;
            private uint _position = uint.MaxValue;

            /// <summary>
            /// Moves this enumerator to the given id.
            /// </summary>
            public void MoveTo(uint id)
            {
                _pointer = _db._pointers[id * 2 + 0];
                _count = _db._pointers[id * 2 + 1];
                _position = uint.MaxValue;
            }

            /// <summary>
            /// Move to the next link.
            /// </summary>
            public bool MoveNext()
            {
                if (_position == uint.MaxValue)
                {
                    _position = 0;
                }
                else
                {
                    _position++;
                }
                return _count > _position;
            }

            /// <summary>
            /// Returns the current # links.
            /// </summary>
            public uint Count
            {
                get
                {
                    return _count;
                }
            }

            /// <summary>
            /// Gets the edge id.
            /// </summary>
            public uint EdgeId
            {
                get
                {
                    return _db._data[_pointer + (_position * 2) + 0];
                }
            }

            /// <summary>
            /// Gets the offset.
            /// </summary>
            public ushort Offset
            {
                get
                {
                    return (ushort)_db._data[_pointer + (_position * 2) + 1];
                }
            }
        }

        /// <summary>
        /// Returns the size in bytes as if serialized.
        /// </summary>
        /// <returns></returns>
        public long SizeInBytes
        {
            get
            {
                var profileBytes = System.Text.UnicodeEncoding.Unicode.GetByteCount(_profileName) + 8;

                return 1 + 8 + 16 + 8 + // the header: the length of the pointers, data and a version-byte.
                    profileBytes +
                    ((long)_pointers.Length) * 4 +
                    ((long)_nextPointer * 4); // the bytes for the actual data.
            }
        }

        /// <summary>
        /// Serializes this trips db to disk.
        /// </summary>
        public long Serialize(Stream stream)
        {
            var position = stream.Position;
            stream.WriteByte(1); // write version #.

            // write guid.
            stream.Write(_id.ToByteArray(), 0, 16);

            // write profile.
            stream.WriteWithSize(_profileName);

            var binaryWriter = new BinaryWriter(stream);
            binaryWriter.Write((long)_pointers.Length);
            binaryWriter.Write((long)_nextPointer); // write size.
            // write pointers.
            for(var i = 0; i < (long)_pointers.Length; i++)
            {
                binaryWriter.Write(_pointers[i]);
            }
            // write data.
            for (var i = 0; i < (long)_nextPointer; i++)
            {
                binaryWriter.Write(_data[i]);
            }
            return stream.Position - position;
        }

        /// <summary>
        /// Deserializes this trips db to disk.
        /// </summary>
        public static StopLinksDb Deserialize(Stream stream)
        {
            if (stream.ReadByte() != 1)
            {
                throw new Exception("Cannot deserialize stop links db, version # doesn't match.");
            }
            
            var guidBytes = new byte[16];
            stream.Read(guidBytes, 0, 16);
            var guid = new Guid(guidBytes);

            var profileName = stream.ReadWithSizeString();

            var binaryReader = new BinaryReader(stream);
            var pointerSize = binaryReader.ReadInt64();
            var dataSize = binaryReader.ReadInt64();

            var pointers = new MemoryArray<uint>(pointerSize);
            pointers.CopyFrom(stream);

            var data = new MemoryArray<uint>(dataSize);
            data.CopyFrom(stream);
            return new StopLinksDb(guid, profileName, pointers, data);
        }
    }
}