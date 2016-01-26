using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.CoreRT
{
    internal class DataTargetReader
    {
        DataTarget _dataTarget;
        long _position;

        public DataTargetReader(DataTarget dataTarget, long initialPosition)
        {
            _dataTarget = dataTarget;
            _position = initialPosition;
        }

        public ushort ReadUInt16()
        {
            AlignUp(2);
            ushort ret = _dataTarget.ReadUInt16(_position);
            _position += 2;
            return ret;
        }

        public uint ReadUInt32()
        {
            AlignUp(4);
            uint ret = _dataTarget.ReadUInt32(_position);
            _position += 4;
            return ret;
        }

        public long ReadPointer()
        {
            AlignUp(PointerSize);
            long ret = _dataTarget.ReadPointer(_position);
            _position += PointerSize;
            return ret;
        }

        public void Skip(int bytesToSkip)
        {
            _position += bytesToSkip;
        }

        public void AlignUp(int alignSize)
        {
            _position = ((_position + alignSize - 1) / alignSize) * alignSize;
        }

        public int PointerSize { get { return _dataTarget.PointerSize; } }

        public long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public DataTarget DataTarget { get { return _dataTarget; } }
    }
}
