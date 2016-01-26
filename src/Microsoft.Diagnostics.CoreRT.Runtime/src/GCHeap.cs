using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.CoreRT
{
    public class GCHeap
    {
        GCDebugContract _gcContract;
        GCWksDebugContract _wksContract;
        long[] _threadAllocContextAddresses;

        internal GCHeap(GCDebugContract gcContract, 
            GCWksDebugContract wksContract,
            EETypeDebugContract eeTypeContract,
            ObjectDebugContract objectContract,
            long[] threadAllocContextAddresses)
        {
            _gcContract = gcContract;
            _wksContract = wksContract;
            _threadAllocContextAddresses = threadAllocContextAddresses;
            Objects = new GCHeapObjectEnumerable(_wksContract, eeTypeContract, objectContract, threadAllocContextAddresses);
            Heaps = InitHeapDataWks(wksContract);
        }

        Heap[] InitHeapDataWks(GCWksDebugContract contract)
        {
            Heap[] heaps = new Heap[1];
            heaps[0] = new Heap();
            heaps[0].Segments = contract.GetHeapSegmentList().
                Select(hs => new Segment()
                {
                    Start = hs.Mem,
                    End = (hs == contract.EphemeralSegment ? contract.AllocAllocated : hs.Allocated),
                }).
                ToArray();

            return heaps;
        }

        /// <summary>
        /// Returns true if an in-progress GC operation has temporarily put the heap data structures
        /// in an inconsistent state. Inspecting the GC in this state may produce undefined results. 
        /// </summary>
        public bool IsRuntimeEditInProgress { get { return !_gcContract.GCStructuresValid; } }

        public Heap[] Heaps { get; private set; }

        public IEnumerable<GCHeapObject> Objects { get; private set; }

        internal class GCHeapObjectEnumerable : IEnumerable<GCHeapObject>
        {
            GCWksDebugContract _contract;
            EETypeDebugContract _eeTypeContract;
            ObjectDebugContract _objectContract;
            long[] _threadAllocContextAddresses;

            public GCHeapObjectEnumerable(GCWksDebugContract contract,
                EETypeDebugContract eeTypeContract,
                ObjectDebugContract objectContract,
                long[] threadAllocContextAddresses)
            {
                _contract = contract;
                _eeTypeContract = eeTypeContract;
                _objectContract = objectContract;
                _threadAllocContextAddresses = threadAllocContextAddresses;
            }

            public IEnumerator<GCHeapObject> GetEnumerator()
            {
                return new GCHeapEnumerator(_contract, _eeTypeContract, _objectContract, _threadAllocContextAddresses);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new GCHeapEnumerator(_contract, _eeTypeContract, _objectContract, _threadAllocContextAddresses);
            }
        }
    }

    public class Heap
    {
        public Segment[] Segments;
    }

    public class Segment
    {
        public long Start;
        public long End;
    }
}
