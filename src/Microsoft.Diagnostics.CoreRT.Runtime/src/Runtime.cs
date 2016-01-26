using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.CoreRT
{
    public class Runtime
    {
        IDataTarget _dataTargetRaw;
        DataTarget _dataTarget;
        long _debugHeaderAddress;
        long _gcDebugContractAddress;
        long _wksGCDebugContractAddress;
        long _runtimeInstanceContractAddress;
        long _threadStoreContractAddress;
        long _threadContractAddress;
        long _objectContractAddress;
        long _eeTypeContractAddress;
        

        Lazy<RuntimeInstance> _runtimeInstance;
        Lazy<ThreadStore> _threadStore;
        Lazy<Thread[]> _threadList;
        Lazy<GCHeap> _gc;

        public Runtime(IDataTarget dataTarget, long debugHeaderAddress)
        {
            _dataTargetRaw = dataTarget;
            _debugHeaderAddress = debugHeaderAddress;
            _runtimeInstance = new Lazy<RuntimeInstance>(InitRuntimeInstance);
            _threadStore = new Lazy<ThreadStore>(InitThreadStore);
            _threadList = new Lazy<Thread[]>(InitThreadList);
            _gc = new Lazy<GCHeap>(InitGC);
            InitContractAddresses();
        }

        public GCHeap GC {  get { return _gc.Value; } }
        private GCHeap InitGC()
        {
            long[] allocContextAddresses = ThreadList.Select(t => t.AllocContextBufferAddress).ToArray();
            GCDebugContract gcContract = new GCDebugContract(new DataTargetReader(_dataTarget, _gcDebugContractAddress));
            GCWksDebugContract gcWksContract = new GCWksDebugContract(new DataTargetReader(_dataTarget, _wksGCDebugContractAddress));
            EETypeDebugContract eeTypeContract = new EETypeDebugContract(new DataTargetReader(_dataTarget, _eeTypeContractAddress));
            ObjectDebugContract objectDebugContract = new ObjectDebugContract(new DataTargetReader(_dataTarget, _objectContractAddress));

            return new GCHeap(gcContract, gcWksContract, eeTypeContract, objectDebugContract, allocContextAddresses);
        }

        internal RuntimeInstance RuntimeInstance {  get { return _runtimeInstance.Value; } }
        private RuntimeInstance InitRuntimeInstance()
        {
            RuntimeInstanceDebugContract contract = new RuntimeInstanceDebugContract(
                new DataTargetReader(_dataTarget, _runtimeInstanceContractAddress));
            return contract.GetRuntimeInstance();
        }

        internal ThreadStore ThreadStore {  get { return _threadStore.Value; } }
        private ThreadStore InitThreadStore()
        {
            ThreadStoreDebugContract contract = new ThreadStoreDebugContract(
                new DataTargetReader(_dataTarget, _threadStoreContractAddress));
            return contract.GetThreadStore(RuntimeInstance.ThreadStore);
        }
        
        internal Thread[] ThreadList {  get { return _threadList.Value; } }
        private Thread[] InitThreadList()
        {
            ThreadDebugContract contract = new ThreadDebugContract(
                new DataTargetReader(_dataTarget, _threadContractAddress));
            const int maxAllowedThreads = 4096;
            List<Thread> threads = new List<Thread>();
            long currentThreadAddress = ThreadStore.ThreadList;
            for(int i = 0; i < maxAllowedThreads; i++)
            {
                if(currentThreadAddress == 0)
                {
                    break;
                }
                Thread t = contract.GetThread(currentThreadAddress);
                currentThreadAddress = t.Next;
                threads.Add(t);
            }
            if(currentThreadAddress != 0)
            {
                throw new BadInputFormatException("Too many threads in thread list, linked list is corrupt");
            }
            return threads.ToArray();
        }


        private void InitContractAddresses()
        {
            //we don't yet know what the endian-ness or pointer size is, but we don't need to.
            //the initial header fields are defined to have little-endian encoding and no pointer
            //sized entries are used.
            DataTarget initialDataTarget = new DataTarget(_dataTargetRaw, 4, false);
            int cookie = initialDataTarget.ReadInt32(_debugHeaderAddress);
            if (cookie != RequiredCookie)
            {
                throw new BadInputFormatException("Debug header does not have expected cookie");
            }
            ushort majorVersion = initialDataTarget.ReadUInt16(_debugHeaderAddress + 4);
            if(majorVersion > MaxMajor)
            {
                throw new UnsupportedVersionException("Debug header version " + majorVersion + " not supported");
            }

            // Parsing the actual minor version is irrelevant at this point
            // It will be at least 0 which is all we understand, and all higher
            // versions are guaranteed to be back-compatible

            // for minor version 0 the remainder of the header is:
            // offset   size                   value
            // 8        4                      Flags
            // 12       4                      ReservedPadding
            int flags = initialDataTarget.ReadInt32(_debugHeaderAddress + 8);
            int targetPointerSize = ((flags & 0x1) == 0x1) ? 8 : 4;
            bool isBigEndian = (flags & 0x2) == 0x2;
            _dataTarget = new DataTarget(_dataTargetRaw, targetPointerSize, isBigEndian);

            // 16  16       target_pointer         gc contract
            // 20  24       target_pointer         gc workstation contract
            // 24  32       target_pointer         runtime instance contract
            // 28  40       target_pointer         thread store contract
            // 32  48       target_pointer         thread contract
            // 36  56       target_pointer         object contract
            // 40  64       target_pointer         EEType contract
            DataTargetReader contractReader = new DataTargetReader(_dataTarget, _debugHeaderAddress + 16);
            _gcDebugContractAddress = contractReader.ReadPointer();
            _wksGCDebugContractAddress = contractReader.ReadPointer();
            _runtimeInstanceContractAddress = contractReader.ReadPointer();
            _threadStoreContractAddress = contractReader.ReadPointer();
            _threadContractAddress = contractReader.ReadPointer();
            _objectContractAddress = contractReader.ReadPointer();
            _eeTypeContractAddress = contractReader.ReadPointer();
            
        }

        const int RequiredCookie = 0x3631666e; // "NF16" ascii bytes but read as a little endian 4 byte integer
        const short MaxMajor = 1;
        const short CurrentMinor = 0;
    }
}