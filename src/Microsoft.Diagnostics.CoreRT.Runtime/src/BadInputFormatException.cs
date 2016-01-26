using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    public class BadInputFormatException : Exception
    {
        public BadInputFormatException(String message)
            : base(message)
        {
        }
    }

    public class NotInitializedException : BadInputFormatException
    {
        public NotInitializedException(string message)
            : base(message)
        {
        }
    }

    public class UnsupportedVersionException : BadImageFormatException
    {
        public UnsupportedVersionException(string message)
            : base(message)
        {
        }
    }
}
