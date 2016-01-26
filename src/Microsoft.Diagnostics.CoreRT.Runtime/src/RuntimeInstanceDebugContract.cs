using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class RuntimeInstanceDebugContract : DebugContractBase
    {
        public RuntimeInstanceDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _offsetOfRuntimeInstanceThreadStore = reader.ReadUInt32();
            _globalRuntimeInstanceAddrAddr = reader.ReadPointer();
        }

        uint _offsetOfRuntimeInstanceThreadStore;
        long _globalRuntimeInstanceAddrAddr;
        

        public RuntimeInstance GetRuntimeInstance()
        {
            long globalRuntimeInstanceAddr = DataTarget.ReadPointer(_globalRuntimeInstanceAddrAddr);
            if(globalRuntimeInstanceAddr == 0)
            {
                throw new NotInitializedException("RuntimeInstance not initialized");
            }
            long threadStore = DataTarget.ReadPointer(globalRuntimeInstanceAddr + _offsetOfRuntimeInstanceThreadStore);
            return new RuntimeInstance(threadStore);
        }
    }

    class RuntimeInstance
    {
        public RuntimeInstance(long threadStore)
        {
            ThreadStore = threadStore;
        }
        public long ThreadStore { get; private set; }
    }
}
