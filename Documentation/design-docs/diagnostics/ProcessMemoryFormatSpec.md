# CoreRT Debugging Runtime Memory Format Specification  #

In order for debuggers and other diagnostic tools to analyze the state of the runtime, the CoreRT runtime defines a precise serialization format for some portions of memory. This allows debuggers to pause a running process and inspect the memory space in a similar manner to parsing a file.

Although fully documented and supported, **_The CoreRT team recommends that debugger tools make use of APIs such as those in Microsoft.Diagnostics.CoreRT.Runtime when possible rather than reading this format directly_**. The memory format may change relatively quickly over time and although tools will be able to  easily detect breaking changes, continuing updates would be necessary to parse the latest format versions. The runtime team intends to prioritize new features, performance and engineering improvements over maintaining long term stability in the memory format. Developers targeting the API will have less frequent breaking changes and be able to leverage ongoing maintenance and updates on the parser from the CoreRT team.


## Addressing ##
This specification treats the entire virtual memory space of a process as if it were randomly accessible array of bytes. It is assumed that all operating systems where CoreRT operates have an API similar in nature to ReadProcessMemory on Windows or ptrace(PTRACE\_PEEKTEXT) on Linux. When this specification refers to data at address X, this can be concretely translated to calling the operating system's ReadProcessMemory/ptrace API using X as the virtual address parameter. When using dump files, the dump format inevitably defines some mapping between file offets and virtual memory addresses. Consult the appropriate dump format specific documentation to determine this mapping or use a specialized dump reading API when working with dump files.

This format does define any meaning for large portions of the address space, nor does it define the full extents of the address space. All addresses not mentioned in this format may or may not be readable. Of the unmentioned addresses that happen to be readable, they are not relevant to parsing this format and may contain arbitrary data.

## Design Notes ##
The serialization format may appear a bit odd relative to other typical file formats. In many places
the format reuses parts of existing C++ runtime data-structures as-is rather than forcing the data to be duplicated or refactored into a seperate structures. Performance and convenience for the runtime will frequently (but not always) take precedence over simplicity of deserialization.

## Locating the Debug Header ##
The Debug Header is the initial entrypoint to the format, however unlike a typical file format it won't reside at address 0. Locating it depends on how the runtime image was linked as well as operating system or dump reading APIs available. Well-known techniques are documented here:

- Windows - Enumerate all loaded images in the process searching for one which contains a named export  called **g_NetNativeRuntimeDebugHeader**. APIs such as CreateToolhelp32Snapshot can locate image base addresses in a loaded process. For dumps consult MiniDumpReadDumpStream API and related windows minidump format specifications to read the module list. The data starting at the image base address is defined by the PE file format specification, including information about parsing named exports. The value of this export is the address of the DebugHeader

- Other platforms - TODO

## Debug Header Part 1##

All addresses for **DebugHeader** parts are given as offsets from the **DebugHeader** address calculated above.

Offset - Size - Value

- 00 - 4 - Cookie
- 04 - 2 - Major Version
- 06 - 2 - Minor Version


**Cookie** - The cookie serves as a sanity check against process corruption or being requested
to treat an invalid address as the DebugHeader. It can also be changed if we want to make a breaking change so drastic that earlier debuggers should treat the module as if it had no .Net runtime at all. If the cookie is valid a debugger is safe to assume the Major/Minor version fields will follow, but any contents beyond that depends on the version values. The cookie value is set to 0x6e, 0x66, 0x31, 0x36 (NF16 in ascii)

**Major Version** - This counter can be incremented to indicate breaking changes. This field is encoded little endian, regardless of the typical endianess of the machine. Current value is 1.

**Minor Version** - This counter can be incremented to indicate back-compatible changes. This field must be encoded little endian, regardless of the typical endianess of the machine. The current value is 0 however a well implemented reader should accept any higher value. Higher values indicate that the format is following a newer version of this specification, but that newer version is guaranteed to work correctly when interpreted as if it was minor version 0.

## Debug Header Part 2

If the DebugHeader Major version > 1, a newer version of this specification will be required to parse any further data. If Major Version == 0 the header is improperly formatted. Otherwise if Major version == 1 the header continues:

Offset - Size - Value

- 08 - 4 - Flags
- 12 - 4 - ReservedPadding

**Flags** - These flags must be encoded as a 4 byte little endian integer, regardless of the typical endianess of the machine. Ie Bit 0 is the least significant bit of the first byte.

  - Bit 0 - Set if the pointer size is 8 bytes, otherwise pointer size is 4 bytes
  - Bit 1 - Set if the machine is big endian

The high 30 bits are reserved and must be ignored regardless of value. Changes to these remaining bits will be considered a back-compatible change. 

**ReservedPadding** - Currently serves as alignment padding for the pointers which  follow but any future usage will be considered a back-compatible change. Readers must ignore this value.

## Debug Header Part 3 ##

All data in the remainder of the format is encoded in the platform specific endianess defined in the **Flags** field unless otherwise specified. Pointer sized fields are should be interpreted using the length defined in the flags field. Offsets will be separately defined whether 4 or 8 byte pointers are in use to account for both pointer size and to limit the need for unaligned reads.

Offset (4 byte pointer) / Offset (8 byte pointer) / Size / Value

- 16 - 16 - Pointer - GC Contract Address
- 20 - 24 - Pointer - Workstation GC Contract Address
- 24 - 32 - Pointer - RuntimeInstance Contract Address
- 28 - 40 - Pointer - Thread Store Contract Address
- 32 - 48 - Pointer - Thread Contract Address

Each contract address field indicates the address where the given contract section can be read from. Each section type is defined below in the specification.

## General Notes for all Contract Sections ##

 **Usage** - Logically diagnostic tools can treat each section as a seperately versioning component within the runtime that will provide some information about its current state. Each contract typically includes both the raw data serialized within the format as well as rules about how this data can be interpreted to do meaningful analysis on the runtime state. Contracts frequently delegate to one another and most diagnostic tasks will require parsing multiple contract sections to get all the data necessary.

**Versioning** - Each Contract Section includes an independent Major and Minor Version. These versions can increment separately from the versions in the **DebugHeader**. A well implemented reader must be prepared to encounter a contract section whose Major version is different than the versions it understands how to parse. The reader can freely decide whether to abandon parsing the process entirely, or to incrementally degrade functionality using only data from the sections it can parse. As with the **DebugHeader**, Minor versions represent back-compatible changes and a reader is free to interpret the section under the specification for any lower minor version. Reader implementers are encouraged to handle parsing both the current version and all past versions, but ultimately it the implementer's choice which format versions are supported.

The versioning in each section covers two types of information:

- The serialization format for the data in that section, such as the offsets, sizes, and byte ordering of data fields.
- Rules for how the data in those fields can be usefully interpreted to analyze runtime state

Including the rules in the versioning ensures that if the runtime changes an important algorithm, diagnostic tools won't unknowingly continue applying the old algorithm and generate incorrect results. For example the runtime could add a new pointer sized field to the end of every managed object and update the object size calculation algorithm to account for it. There is no data in the entire memory serialization format (other than a version number) which would be changed by this. However the Workstation GC contract currently includes a rule about how to calculate object size, and changing the rule would be a breaking change that necessitates a new MajorVersion for that contract.

Diagnostic tools need to be very careful when using private knowledge about how the runtime operates that isn't explicitly defined in this specification. Microsoft may change the runtime operation without warning and tools relying on private knowledge may fail. Instead of doing this, contact us to ask that additional information be added to the specification or let us work with you to try identifying alternative better supported approaches. We can even retroactively add rules to previous versions of the contract sections, as long we determine that rule was already true at the time and we intend to version changes to it going forward.

The addresses in each contract section are specified as offsets from the relevant contract address encoded in the **DebugHeader**. The initial 4 bytes of all contract sections are:

Offset / Size / Value

- 0 - 2 - Major Version
- 2 - 2 - Minor Version

**Major Version** - Major version as described above. Major version 0 has special meaning that the process has not yet initialized this portion of the data. Although unusable now, further execution of the process may initialize the data to a usable state.

**Minor Version** - Minor version as described above

## GC Contract Section (Version 1.0) ##

This section helps determine if the data in **Workstation GC Contract Section** is currently trustworthy.

Offset (4 byte pointer) / Offset (8 byte pointer) / Size / Value

- 4 - 8 - Pointer - m_GcStructuresInvalidCntAddr

**m\_GcStructuresInvalidCntAddr** - The address of a 4 byte unsigned integer. When this integer is non-zero the runtime is making updates to GC data structures that may leave them temporarily inconsistent. The reader should avoid relying on any data or rules defined in the **Workstation GC Contract** while in this state.


## Workstation GC Contract Section (Version 1.0) ##

This section helps enumerate managed heaps, segments, and objects when the workstation flavor of GC is running. Currently this is the only supported flavor of GC in CoreRT. Be sure to check the **GC Contract Section** to determine if the information in this section is currently trustworthy. 

The CoreRT Workstation GC can be treated as a heap which has a collection of segments which in turn contain managed objects. Allocation contexts are small ranges within a segment that are reserved for future allocations and have no managed objects yet. For more background information about the GC please refer to the .Net GC design documents.

Logically this contract section determines how to decode portions of the following runtime data structures and globals which can then be used to navigate the heap:

- **alloc_context** - Defines a range of memory where new managed object allocations will occur in the future. No managed objects reside in this range now.

	- alloc_ptr - Pointer - The start address of the range (inclusive)
	
	- alloc_limit - Pointer - The end address of the range (exclusive)

- **heap_segment** - Defines a range of memory where managed objects are currently allocated. *alloc\_contexts* may specify subsets of this range that are vacant.

	- mem - Pointer -The start address of the range

	- allocated - Pointer - The end address of the range

	- next - Pointer - The next *heap\_segment* in a linked list of segments

- **generation** - This data structure doesn't have a useful definition in the scope of this specification other than noting that certain fields of certain instances will be useful to enumerate the collection of *heap_segments* and to enumerate managed objects.

	- allocation\_context - An *alloc_context*

	- allocation\_start - Pointer - The start address of the generation's memory range

	- start\_segment - Pointer - The address of the first *heap_segment* in a linked list of segments containing objects from this generation.

- **generation_table** - A global array of *generation* data structures

- **alloc\_allocated** - Pointer - The global end address range of the ephemeral segment, a unique *heap\_segment* that requires special case handling

- **ephemeral\_heap\_segment** - Pointer - The global address of the ephemeral segment, a unique *heap_segment* that requires special case handling
	

**Contract section format**

Offset (4 byte pointer) / Offset (8 byte pointer) / Size / Value

 - 04 - 08 - Pointer - generation\_table\_addr
 - 08 - 16 - Pointer - alloc\_allocated\_addr
 - 12 - 24 - Pointer - ephemeral\_heap\_segment\_addr
 - 16 - 32 - 4 - number\_generations
 - 20 - 36 - 4 - offset\_of\_generation\_allocation\_context
 - 24 - 40 - 4 - offset\_of\_generation\_allocation\_start
 - 28 - 44 - 4 - offset\_of\_generation\_start\_segment
 - 32 - 48 - 4 - size\_of\_generation
 - 36 - 52 - 4 - offset\_of\_heap\_segment\_mem
 - 40 - 56 - 4 - offset\_of\_heap\_segment\_allocated
 - 44 - 60 - 4 - offset\_of\_heap\_segment\_next
 - 48 - 64 - 4 - offset\_of\_alloc\_context\_alloc\_ptr
 - 52 - 68 - 4 - offset\_of\_alloc\_context\_alloc\_limit
 - 56 - 72 - 4 - min\_object\_size

**generation\_table\_addr** - The address of the *generation_table*, a sequence of **number\_generations** *generation* data structures encoded sequentially. 

**alloc\_allocated\_addr** - The address of the pointer typed global *alloc_allocated*

**ephemeral\_heap\_segment\_addr** - The address of the pointer typed global *ephemeral\_heap\_segment*

**number\_generations** - The number of *generation* data structures in the *generation_table*

**offset\_of\_generation\_allocation\_context** - The address offset of the *allocation\_context* field from the start of a *generation*.

**offset\_of\_generation\_allocation\_start** - The address offset of the *allocation_start* field from the start of a *generation*.

**offset\_of\_generation\_start\_segment** - The address offset of the *start_segment* field from the start of a *generation*

**size\_of\_generation** - The address offset from the start of one *generation* to the start of the next one in the *generation_table*.

**offset\_of\_heap\_segment\_mem** - The address offset of the *mem* field from the start of a *heap_segment*

**offset\_of\_heap\_segment\_allocated** - The address offset of the *allocated* field from the start of a *heap_segment*

**offset\_of\_heap\_segment\_next** - The address offset of the *next* field from the start of a *heap_segment*

**offset\_of\_alloc\_context\_alloc\_ptr** - The address offset of the *alloc_ptr* field from the start of a *alloc_context*

**offset\_of\_alloc\_context\_alloc\_limit** - The address offset of the *alloc_limit* field from the start of an *alloc_context*

**min\_object\_size** - The size of a minimal object, this is a global constant needed to enumerate all managed objects on the heap


**How to Enumerate all Heap Segments**

The *start_segment* fields of *generation\_table[number\_generations-1]* and *generation\_table[number\_generations-2]* are the starting addresses of two linked lists of *heap\_segments*. Iterate through each list using the *heap\_segment.next* field. A *next* value of 0 signals the end of the list. Merge both lists to obtain all the *heap\_segments* in the heap.

**How to calculate the size of an object**

Given the starting address of an object, interpret the first pointer size set of bytes as the address of a *MethodTable*. The 4 bytes at offset 4 of the *MethodTable* is an unsigned integer *base\_size*. The 2 bytes at offset 0 are an unsigned integer *component\_size*. If *component\_size* is non-zero, read *component\_count*, a 4 byte unsigned integer at offset pointer\_size in the object. The *unaligned_size* is computed as *base\_size* + (*component\_count***component\_size*). If the object address is contained in a *heap\_segment* within the linked list *generation\_table[number\_generations-1].start\_segment* then the align the size up to the nearest 8 bytes. Otherwise align the size up to the nearest multiple of pointer\_size.

TODO: factor some of those offsets into the contract

**How to enumerate all objects in a heap\_segment**

First determine all the address of all thread-specific *alloc\_context* buffer addresses, by enumerating all threads. The **Runtime Instance Contract**, **ThreadStore Contract**, and **Thread Contract** define how to do this. For each *alloc\_context* buffer address, interpret the contents as an *alloc\_context*. Append the *alloc\_context* from *generation\_table[0].allocation\_context* to this list. Let *cur\_address* = *heap\_segment.mem*. If *cur\_address* is within the range of one of the *alloc_contexts*, set *cur\_address* = *alloc\_context.limit* + *min\_object\_size*. Otherwise interpret *cur\_address* as the start of an object, and advance *cur\_address* by the object size. Repeat this until *cur\_address* >= *heap\_segment.allocated*. If the *heap\_segment* address matches the global *ephemeral\_heap\_segment* then repeat until *cur\_address* >= *alloc\_allocated*

**How to enumerate all objects in the heap**

Enumerate all *heap\_segments* and then for each segment enumerate all the objects within it.

**Performance optimization for determining if an object address is within an alloc\_context**

All *alloc\_context* memory ranges are within the range [*generation\_table[0].allocation\_start*, *alloc\_allocated*). By checking against this single larger range the enumeration algorithm can quickly rule out most objects from being within any *alloc\_context*.

## Runtime Instance Contract Section (Version 1.0) ##

TODO

## ThreadStore Contract Section (Version 1.0) ##

TODO

## Thread Contract Section (Version 1.0) ##

TODO


## Object Contract Section (Version 1.0)

This section helps decoding the memory representation of managed objects.

## EEType Contract Section (Version 1.0)

This section helps decode the runtime EEType datastructure, which contains type related information

Offset (4 byte pointer) / Offset (8 byte pointer) / Size / Value

 - 04 - 04 - 4 - OffsetOfEETypeBaseSize
 - 08 - 08 - 4 - OffsetOfEETypeComponentSize



