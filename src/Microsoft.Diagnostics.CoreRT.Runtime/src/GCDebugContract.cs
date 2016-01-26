using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class GCDebugContract : DebugContractBase
    {
        public GCDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _gcStructuresInvalidCntAddr = reader.ReadPointer();
        }

        long _gcStructuresInvalidCntAddr;

        public bool GCStructuresValid
        {
            get
            {
                return 0 == DataTarget.ReadUInt32(_gcStructuresInvalidCntAddr);
            }
        }
    }
}
