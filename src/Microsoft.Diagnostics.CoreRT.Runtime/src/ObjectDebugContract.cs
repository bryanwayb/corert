using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class ObjectDebugContract : DebugContractBase
    {
        public ObjectDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _offsetOfObjectEEType = reader.ReadUInt32();
            _offsetOfArrayLength = reader.ReadUInt32();
        }

        uint _offsetOfObjectEEType;
        uint _offsetOfArrayLength;

        public uint GetEETypeAndUnalignedSize(long objectAddress, EETypeDebugContract eeTypeDebugContract, out long eeTypeAddress)
        {
            eeTypeAddress = DataTarget.ReadPointer(objectAddress + _offsetOfObjectEEType);
            EEType eeType = eeTypeDebugContract.ReadEEType(eeTypeAddress);
            if(eeType.ComponentSize != 0)
            {
                return eeType.BaseSize + (eeType.ComponentSize * DataTarget.ReadUInt32(objectAddress + _offsetOfArrayLength));
            }
            else
            {
                return eeType.BaseSize;
            }
        }
    }
}
