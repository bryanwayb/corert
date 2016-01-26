using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class ThreadStoreDebugContract : DebugContractBase
    {
        public ThreadStoreDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _offsetOfThreadStoreThreadList = reader.ReadUInt32();
        }

        uint _offsetOfThreadStoreThreadList;
        

        public ThreadStore GetThreadStore(long threadStoreAddress)
        {
            long threadList = DataTarget.ReadPointer(threadStoreAddress + _offsetOfThreadStoreThreadList);
            return new ThreadStore(threadList);
        }
    }

    class ThreadStore
    {
        public ThreadStore(long threadList)
        {
            ThreadList = threadList;
        }
        public long ThreadList { get; private set; }
    }
}
