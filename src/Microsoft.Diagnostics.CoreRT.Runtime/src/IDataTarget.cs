using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    public interface IDataTarget
    {
        int ReadMemory(long address, byte[] memoryBytes);
    }
}
