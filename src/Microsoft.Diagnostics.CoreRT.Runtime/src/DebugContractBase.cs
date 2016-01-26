using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    internal class DebugContractBase
    {
        public DebugContractBase(DataTargetReader reader, int supportedMajorVersion)
        {
            MajorVersion = reader.ReadUInt16();
            MinorVersion = reader.ReadUInt16();

            if(MajorVersion == 0)
            {
                throw new NotInitializedException("Contract not yet initialized");
            }
            if(MajorVersion > supportedMajorVersion)
            {
                throw new UnsupportedVersionException("Contract version is unsupported");
            }

            DataTarget = reader.DataTarget;
        }



        public ushort MajorVersion { get; private set; }
        public ushort MinorVersion { get; private set; }
        public DataTarget DataTarget { get; private set; }
    }
}
