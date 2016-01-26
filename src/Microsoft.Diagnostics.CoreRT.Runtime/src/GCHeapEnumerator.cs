using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Diagnostics.CoreRT
{
    internal class GCHeapEnumerator : IEnumerator<GCHeapObject>
    {
        struct EnumeratorSegment
        {
            public long Start;
            public long End;
            public bool RequiresAlign8;
        }
        struct EnumeratorHeap
        {
            public long Gen0Start;
            public long Gen0End;
            public AllocContext DefaultAllocationContext;
            public EnumeratorSegment[] Segments;
        }

        EETypeDebugContract _eeTypeContract;
        ObjectDebugContract _objectContract;
        DataTarget _dataTarget;
        AllocContext[] _allocContexts;
        EnumeratorHeap[] _heaps;
        uint _minObjectSize;

        long _currentObj;
        uint _currentObjSize;
        long _currentObjEEType;

        int _currentHeapIndex;
        int _currentSegmentIndex;

        internal GCHeapEnumerator(GCWksDebugContract wksGCContract,
            EETypeDebugContract eeTypeContract,
            ObjectDebugContract objectContract,
            long[] allocContextAddresses)
        {
            _eeTypeContract = eeTypeContract;
            _objectContract = objectContract;
            _dataTarget = wksGCContract.DataTarget;
            _allocContexts = allocContextAddresses.Select(addr => wksGCContract.ReadAllocationContext(addr)).ToArray();
            _heaps = InitHeapsForWorkstation(wksGCContract);
            _minObjectSize = wksGCContract.MinObjectSize;
            Reset();
        }

        public GCHeapObject Current
        {
            get
            {
                return new GCHeapObject(_currentObj, _currentObjEEType, (int)_currentObjSize);
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return new GCHeapObject(_currentObj, _currentObjEEType, (int)_currentObjSize);
            }
        }

        public bool MoveNext()
        {
            while(true)
            {
                // There are two cases:
                // a) mCurrSize > 0. A previous iteration of this loop (perhaps in a prior invocation of MoveNext)
                // identified a valid object and calculated its size. We've already enumerated it and now we need to
                // skip past it
                // b) mCurrSize = 0. We are either in the initial state of the enumerator, or a previous iteration of the
                // loop advanced the pointer without checking if an object is present. We want to check this current
                // pointer as-is.
                _currentObj += _currentObjSize;
                _currentObjSize = 0;
                _currentObjEEType = 0;

                // if we moved off the segment end, advance to the next valid heap and segment
                if (_currentObj >= _heaps[_currentHeapIndex].Segments[_currentSegmentIndex].End)
                {
                    _currentSegmentIndex++;
                    while (_currentSegmentIndex >= _heaps[_currentHeapIndex].Segments.Length)
                    {
                        _currentSegmentIndex = 0;
                        _currentHeapIndex++;
                        if (_currentHeapIndex >= _heaps.Length)
                        {
                            return false;
                        }
                    }
                    _currentObj = _heaps[_currentHeapIndex].Segments[_currentSegmentIndex].Start;
                    continue;
                }

                // Inside gen0 we need to skip past allocation contexts
                if (_heaps[_currentHeapIndex].Gen0Start <= _currentObj &&
                    _heaps[_currentHeapIndex].Gen0End > _currentObj &&
                    SkipOverAllocationContextsIfNeeded())
                {
                    continue;
                }

                try
                {
                    _currentObjSize = GetSizeAndEEType(_currentObj, out _currentObjEEType);
                }
                catch(MemoryReadException)
                {
                    return false;
                }

                // found an object to enumerate
                return true;
            } 
            //unreachable
        }

        public void Reset()
        {
            _currentObj = _heaps[0].Segments[0].Start;
            _currentObjEEType = 0;
            _currentObjSize = 0;
            _currentHeapIndex = 0;
            _currentSegmentIndex = 0;
        }

        public void Dispose()
        {
            _eeTypeContract = null;
            _objectContract = null;
            _dataTarget = null;
        }

        private uint GetSizeAndEEType(long objectAddress, out long eeTypeAddress)
        {
            uint size = _objectContract.GetEETypeAndUnalignedSize(objectAddress, _eeTypeContract, out eeTypeAddress);

            if (_heaps[_currentHeapIndex].Segments[_currentSegmentIndex].RequiresAlign8)
            {
                size = Align8(size);
            }
            else
            {
                size = AlignPointer(size);
            }
            return size;
        }

        static EnumeratorHeap[] InitHeapsForWorkstation(GCWksDebugContract contract)
        {
            EnumeratorHeap heap = new EnumeratorHeap();
            IEnumerable<EnumeratorSegment> sohSegments = contract.GetHeapSegmentList(contract.OldestSOHGeneration).
                Select(hs => new EnumeratorSegment()
                {
                    Start = hs.Mem,
                    End = (hs == contract.EphemeralSegment ? contract.AllocAllocated : hs.Allocated),
                    RequiresAlign8 = false
                });
            IEnumerable<EnumeratorSegment> lohSegments = contract.GetHeapSegmentList(contract.LOHGeneration).
                Select(hs => new EnumeratorSegment()
                {
                    Start = hs.Mem,
                    End = hs.Allocated,
                    RequiresAlign8 = true
                });
            heap.Segments = sohSegments.Union(lohSegments).ToArray();
            Generation[] table = contract.ReadGenerationTable();
            heap.Gen0Start = table[0].AllocationStart;
            heap.Gen0End = contract.AllocAllocated;
            heap.DefaultAllocationContext = table[0].AllocationContext;
            return new EnumeratorHeap[] { heap };
        }

        bool SkipOverAllocationContextsIfNeeded()
        {
            int MinObjSize = _dataTarget.PointerSize * 3;
            bool addressMoved = false;
            
            foreach (AllocContext allocContext in _allocContexts)
            {
                if (_currentObj == allocContext.AllocPtr)
                {
                    _currentObj = allocContext.AllocLimit + MinObjSize;
                    addressMoved = true;
                }
            }
            
            if (_currentObj == _heaps[_currentHeapIndex].DefaultAllocationContext.AllocPtr)
            {
                _currentObj = _heaps[_currentHeapIndex].DefaultAllocationContext.AllocLimit + MinObjSize;
                addressMoved = true;
            }
            return addressMoved;
            
        }

        static uint Align8(uint size)
        {
            return (size + 7) & ~7U;
        }

        uint AlignPointer(uint size)
        {
            if (_dataTarget.PointerSize == 4)
                return (size + 3) & ~3U;
            else
                return (size + 7) & ~7U;
        }
    }

    public struct GCHeapObject
    {
        public GCHeapObject(long address, long eeType, int size)
        {
            Address = address;
            EEType = eeType;
            Size = size;
        }

        public long Address { get; private set; }
        public long EEType { get; private set; }

        public int Size { get; private set; }
    }
}
