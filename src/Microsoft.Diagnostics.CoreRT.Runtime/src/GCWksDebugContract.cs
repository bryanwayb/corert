using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class GCWksDebugContract : DebugContractBase
    {
        // contract fields in target
        long _generationTableAddress;
        long _allocAllocatedAddress;
        long _ephemeralHeapSegmentAddress;
        uint _numberGenerations;
        uint _offsetOfGenerationAllocationContext;
        uint _offsetOfGenerationAllocationStart;
        uint _offsetOfGenerationStartSegment;
        uint _sizeOfGeneration;
        uint _offsetOfHeapSegmentMem;
        uint _offsetOfHeapSegmentAllocated;
        uint _offsetOfHeapSegmentNext;
        uint _offsetOfAllocContextAllocPtr;
        uint _offsetOfAllocContextAllocLimit;
        uint _minObjectSize;

        // heap segment read cache
        Dictionary<long, HeapSegment> _addressToHeapSegment;

        public GCWksDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _generationTableAddress = reader.ReadPointer();
            _allocAllocatedAddress = reader.ReadPointer();
            _ephemeralHeapSegmentAddress = reader.ReadPointer();
            _numberGenerations = reader.ReadUInt32();
            _offsetOfGenerationAllocationContext = reader.ReadUInt32();
            _offsetOfGenerationAllocationStart = reader.ReadUInt32();
            _offsetOfGenerationStartSegment = reader.ReadUInt32();
            _sizeOfGeneration = reader.ReadUInt32();
            _offsetOfHeapSegmentMem = reader.ReadUInt32();
            _offsetOfHeapSegmentAllocated = reader.ReadUInt32();
            _offsetOfHeapSegmentNext = reader.ReadUInt32();
            _offsetOfAllocContextAllocPtr = reader.ReadUInt32();
            _offsetOfAllocContextAllocLimit = reader.ReadUInt32();
            _minObjectSize = reader.ReadUInt32();

            _addressToHeapSegment = new Dictionary<long, HeapSegment>();
        }



        public Generation[] ReadGenerationTable()
        {
            Generation[] generationTable = new Generation[_numberGenerations];
            long entryAddress = _generationTableAddress;
            for (int i = 0; i < generationTable.Length; i++, entryAddress += _sizeOfGeneration)
            {
                generationTable[i] = ReadGeneration(entryAddress);
            }
            return generationTable;
        }

        public AllocContext ReadAllocationContext(long address)
        {
            return new AllocContext(
                DataTarget.ReadPointer(address + _offsetOfAllocContextAllocPtr),
                DataTarget.ReadPointer(address + _offsetOfAllocContextAllocLimit));
        }

        public Generation ReadGeneration(long address)
        {
            return new Generation(
                ReadAllocationContext(address + _offsetOfGenerationAllocationContext),
                DataTarget.ReadPointer(address + _offsetOfGenerationAllocationStart),
                DataTarget.ReadPointer(address + _offsetOfGenerationStartSegment));
        }

        private HeapSegment ReadHeapSegmentUnCached(long address)
        {
            return new HeapSegment(
                DataTarget.ReadPointer(address + _offsetOfHeapSegmentMem),
                DataTarget.ReadPointer(address + _offsetOfHeapSegmentAllocated),
                DataTarget.ReadPointer(address + _offsetOfHeapSegmentNext));
        }

        public HeapSegment ReadHeapSegment(long address)
        {
            HeapSegment hs;
            if (!_addressToHeapSegment.TryGetValue(address, out hs))
            {
                hs = ReadHeapSegmentUnCached(address);
                _addressToHeapSegment[address] = hs;
            }
            return hs;
        }
        

        public uint LOHGeneration { get { return _numberGenerations - 1; } }
        public uint OldestSOHGeneration { get { return _numberGenerations - 2; } }

        public IEnumerable<HeapSegment> GetHeapSegmentList(uint generation)
        {
            return GetHeapSegmentList(new uint[] { generation });
        }

        public IEnumerable<HeapSegment> GetHeapSegmentList(uint[] generationList = null)
        {
            if(generationList == null)
            {
                generationList = new uint[] { LOHGeneration, OldestSOHGeneration };
            }
            Generation[] table = ReadGenerationTable();
            List<HeapSegment> segments = new List<HeapSegment>();
            foreach (uint generation in generationList)
            {
                long segmentAddr = table[generation].StartSegment;
                while (segmentAddr != 0)
                {
                    if (segments.Count == 1000)
                    {
                        throw new BadInputFormatException("Segment list is corrupt, more than 1000 segments found");
                    }
                    HeapSegment seg = ReadHeapSegment(segmentAddr);
                    segments.Add(seg);
                    segmentAddr = seg.Next;
                }
            }
            return segments.ToArray();
        }

        public long AllocAllocated { get { return DataTarget.ReadPointer(_allocAllocatedAddress); } }
        public HeapSegment EphemeralSegment {  get { return ReadHeapSegment(DataTarget.ReadPointer(_ephemeralHeapSegmentAddress)); } }

        public uint MinObjectSize { get { return _minObjectSize; } }
    }

    class AllocContext
    {
        public AllocContext(long allocPtr, long allocLimit)
        {
            AllocPtr = allocPtr;
            AllocLimit = allocLimit;
        }

        public long AllocPtr { get; private set; }
        public long AllocLimit { get; private set; }
    }

    class Generation
    {
        public Generation(AllocContext allocationContext, long allocationStart, long startSegment)
        {
            AllocationContext = allocationContext;
            AllocationStart = allocationStart;
            StartSegment = startSegment;
        }

        public AllocContext AllocationContext { get; private set; }
        public long AllocationStart { get; private set; }
        public long StartSegment { get; private set; }
    }

    class HeapSegment
    {
        public HeapSegment(long mem, long allocated, long next)
        {
            Mem = mem;
            Allocated = allocated;
            Next = next;
        }

        public long Mem { get; private set; }
        public long Allocated { get; private set; }
        public long Next { get; private set; }
    }
}
