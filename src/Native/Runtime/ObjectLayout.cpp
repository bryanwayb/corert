//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Implementations of functions dealing with object layout related types.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "assert.h"
#include "RedhawkWarnings.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "TargetPtrs.h"
#include "eetype.h"
#include "ObjectLayout.h"

#ifndef DACCESS_COMPILE
void Object::InitEEType(EEType * pEEType)
{
    ASSERT(NULL == m_pEEType);
    m_pEEType = pEEType;
}
#endif

UInt32 Array::GetArrayLength()
{
    return m_Length;
}

void* Array::GetArrayData()
{
    UInt8* pData = (UInt8*)this;
    pData += (get_EEType()->get_BaseSize() - sizeof(ObjHeader));
    return pData;
}

#ifndef DACCESS_COMPILE
void Array::InitArrayLength(UInt32 length)
{
    ASSERT(NULL == m_Length);
    m_Length = length;
}

void MDArray::InitMDArrayLength(UInt32 length)
{
    ASSERT(NULL == m_Length);
    m_Length = length;
}

void MDArray::InitMDArrayDimension(UInt32 dimension, UInt32 value)
{
    ASSERT(m_Dimensions[dimension] == NULL);
    m_Dimensions[dimension] = value;
}

void ObjHeader::SetBit(UInt32 uBit)
{
    PalInterlockedOr(&m_uSyncBlockValue, uBit);
}

void ObjHeader::ClrBit(UInt32 uBit)
{
    PalInterlockedAnd(&m_uSyncBlockValue, ~uBit);
}

size_t Object::GetSize()
{
    EEType * pEEType = get_EEType();

    // strings have component size2, all other non-arrays should have 0
    ASSERT(( pEEType->get_ComponentSize() <= 2) || pEEType->IsArray());

    size_t s = pEEType->get_BaseSize();
    UInt16 componentSize = pEEType->get_ComponentSize();
    if (componentSize > 0)
        s += ((Array*)this)->GetArrayLength() * componentSize;
    return s;
}


// This structure is part of a in-memory serialization format that is used by diagnostic tools to
// reason about the runtime. As a contract with our diagnostic tools it must be kept up-to-date
// by changing the MajorVersion when breaking changes occur. If you are changing the runtime then
// you are responsible for understanding what changes are breaking changes. You can do this by
// reading the specification (Documentation\design-docs\diagnostics\ProcessMemoryFormatSpec.md) 
// to understand what promises the runtime makes to diagnostic tools. Any change that would make that
// document become inaccurate is a breaking change.
//
// If you do want to make a breaking change please coordinate with diagnostics team as breaking changes
// require debugger side components to be updated, and then the new versions will need to be distributed
// to customers. Ideally you will check in updates to the runtime components, the debugger parser
// components, and the format specification at the same time.
// 
// Although not guaranteed to be exhaustive, at a glance these are some potential breaking changes:
//   - Removing a field from this structure
//   - Reordering fields in the structure
//   - Changing the data type of a field in this structure
//   - Changing the data type of a field in another structure that is being refered to here with
//       the offsetof() operator
//   - Changing the data type of a global whose address is recorded in this structure
//   - Changing the meaning of a field or global refered to in this structure so that it can no longer
//     be used in the manner the format specification describes.
struct ObjectDebugContract
{
	const uint16_t MajorVersion = 1; // breaking changes
	const uint16_t MinorVersion = 0; // back-compatible changes
	const uint32_t OffsetOfObjectEEType = offsetof(Object, m_pEEType);
	const uint32_t OffsetOfArrayLength = offsetof(Array, m_Length);
};

extern const ObjectDebugContract g_ObjectDebugContract = {};

#endif
