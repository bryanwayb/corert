using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    public class MemoryReadException : Exception
    {
        public MemoryReadException(long address, long size)
        {
            Address = address;
            Size = size;
        }

        public long Address { get; private set; }
        public long Size { get; private set; }
    }

    internal class DataTarget
    {
        IDataTarget _dataTarget;
        int _pointerSize;
        bool _isBigEndian;
        byte[] _readBuffer1;
        byte[] _readBuffer2;
        byte[] _readBuffer4;
        byte[] _readBuffer8;

        public DataTarget(IDataTarget dataTarget, int pointerSize, bool isBigEndian)
        {
            _dataTarget = dataTarget;
            _pointerSize = pointerSize;
            _isBigEndian = isBigEndian;
            if(_isBigEndian)
            {
                throw new NotImplementedException();
            }
            _readBuffer1 = new byte[1];
            _readBuffer2 = new byte[2];
            _readBuffer4 = new byte[4];
            _readBuffer8 = new byte[8];
        }

        public int PointerSize
        {
            get { return _pointerSize; }
        }

        public bool IsBigEndian
        {
            get { return _isBigEndian; }
        }

        public byte ReadUInt8(long address)
        {
            if (1 != _dataTarget.ReadMemory(address, _readBuffer1))
            {
                throw new MemoryReadException(address, 1);
            }
            else
            {
                return _readBuffer1[0];
            }
        }

        public ushort ReadUInt16(long address)
        {
            if (2 != _dataTarget.ReadMemory(address, _readBuffer2))
            {
                throw new MemoryReadException(address, 2);
            }
            else
            {
                return BitConverter.ToUInt16(_readBuffer2, 0);
            }
        }

        public int ReadInt32(long address)
        {
            if(4 != _dataTarget.ReadMemory(address, _readBuffer4))
            {
                throw new MemoryReadException(address, 4);
            }
            else
            {
                return BitConverter.ToInt32(_readBuffer4, 0);
            }
        }

        public uint ReadUInt32(long address)
        {
            if (4 != _dataTarget.ReadMemory(address, _readBuffer4))
            {
                throw new MemoryReadException(address, 4);
            }
            else
            {
                return BitConverter.ToUInt32(_readBuffer4, 0);
            }
        }

        public long ReadPointer(long address)
        {
            if (_pointerSize == 4)
            {
                if (4 != _dataTarget.ReadMemory(address, _readBuffer4))
                {
                    throw new MemoryReadException(address, 4);
                }
                else
                {
                    return (long)BitConverter.ToUInt32(_readBuffer4, 0);
                }
            }
            else
            {
                if (8 != _dataTarget.ReadMemory(address, _readBuffer8))
                {
                    throw new MemoryReadException(address, 8);
                }
                else
                {
                    return (long)BitConverter.ToInt64(_readBuffer8, 0);
                }
            }
        }

        public bool TryReadPointer(long address, out long value)
        {
            value = 0;
            if (_pointerSize == 4)
            {
                if (4 != _dataTarget.ReadMemory(address, _readBuffer4))
                {
                    return false;
                }
                else
                {
                    value = BitConverter.ToUInt32(_readBuffer4, 0);
                    return true;
                }
            }
            else
            {
                if (8 != _dataTarget.ReadMemory(address, _readBuffer8))
                {
                    return false;
                }
                else
                {
                    value = (long)BitConverter.ToInt64(_readBuffer8, 0);
                    return true;
                }
            }
        }
    }
}
