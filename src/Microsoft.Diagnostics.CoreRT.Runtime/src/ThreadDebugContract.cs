using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class ThreadDebugContract : DebugContractBase
    {
        public ThreadDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _offsetOfThreadNext = reader.ReadUInt32();
            _offsetOfThreadAllocContextBuffer = reader.ReadUInt32();
        }

        uint _offsetOfThreadNext;
        uint _offsetOfThreadAllocContextBuffer;
        

        public Thread GetThread(long threadAddress)
        {
            long next = DataTarget.ReadPointer(threadAddress + _offsetOfThreadNext);
            long allocContextBufferAddress = threadAddress + _offsetOfThreadAllocContextBuffer;
            return new Thread(next, allocContextBufferAddress);
        }
    }

    class Thread
    {
        public Thread(long next, long allocContextBufferAddress)
        {
            Next = next;
            AllocContextBufferAddress = allocContextBufferAddress;
        }
        public long Next { get; private set; }
        public long AllocContextBufferAddress { get; private set; }
    }
}
