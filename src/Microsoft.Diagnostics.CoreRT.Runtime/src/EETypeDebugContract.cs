using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    class EETypeDebugContract : DebugContractBase
    {
        public EETypeDebugContract(DataTargetReader reader) : base(reader, 1)
        {
            _offsetOfEETypeBaseSize = reader.ReadUInt32();
            _offsetOfEETypeComponentSize = reader.ReadUInt32();
            _addressToEEType = new Dictionary<long, EEType>();
        }

        uint _offsetOfEETypeBaseSize;
        uint _offsetOfEETypeComponentSize;

        // method table read cache
        Dictionary<long, EEType> _addressToEEType;

        private EEType ReadEETypeUncached(long eeTypeAddress)
        {
            return new EEType(
                DataTarget.ReadUInt32(eeTypeAddress + _offsetOfEETypeBaseSize),
                DataTarget.ReadUInt16(eeTypeAddress + _offsetOfEETypeComponentSize));
        }

        public EEType ReadEEType(long address)
        {
            EEType eeType;
            if (!_addressToEEType.TryGetValue(address, out eeType))
            {
                eeType = ReadEETypeUncached(address);
                _addressToEEType[address] = eeType;
            }
            return eeType;
        }
    }

    class EEType
    {
        public EEType(uint baseSize, ushort componentSize)
        {
            BaseSize = baseSize;
            ComponentSize = componentSize;
        }
        public uint BaseSize { get; private set; }
        public ushort ComponentSize { get; private set; }
    }
}
