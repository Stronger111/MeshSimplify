﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Chaos
{

    public static class UnsafeUtil
    {
        public static float UintToFloat(uint u)
        {
            unsafe
            {
                return *(float*)&u;
            }
        }

        public static uint FloatToUint(float f)
        {
            unsafe
            {
                return *(uint*) &f;
            }
        }
		[StructLayout( LayoutKind.Sequential )]
		private struct ArrayHeader {
			internal IntPtr type;
			internal int length;
		}

        private unsafe static void HackArraySizeCall<TA>(TA[] array, ArrayHeader* header, int size, Action<TA[]> func)
        {
            int oriLen = header->length;
            header->length = size;
            try
            {
                func(array);
            }
            finally
            {
                header->length = oriLen;
            }
        }

        public unsafe static void IntegerHackArraySizeCall( int[] array, int size, Action<int[]> func) 
		{
			if ( array != null && size < array.Length) {
				fixed ( void* p = array )
				{
				    HackArraySizeCall(array, ((ArrayHeader*) p) - 1, size, func);
				    return;
				}
            }
            func(array);
        }
		public unsafe static void Vector2HackArraySizeCall( Vector2[] array, int size, Action<Vector2[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, ((ArrayHeader*)p) - 1, size, func);
                    return;
                }
            }
            func(array);
        }
		public unsafe static void Vector3HackArraySizeCall( Vector3[] array, int size, Action<Vector3[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, ((ArrayHeader*)p) - 1, size, func);
                    return;
                }
            }
            func(array);
        }
		public unsafe static void Vector4HackArraySizeCall( Vector4[] array, int size, Action<Vector4[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, ((ArrayHeader*)p) - 1, size, func);
                    return;
                }
            }
            func(array);
        }
		public unsafe static void Color32HackArraySizeCall( Color32[] array, int size, Action<Color32[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, ((ArrayHeader*)p) - 1, size, func);
                    return;
                }
            }
            func(array);
        }
		public unsafe static void BoneWeightHackArraySizeCall( BoneWeight[] array, int size, Action<BoneWeight[]> func)
        {
            if (array != null && size < array.Length)
            {
                fixed (void* p = array)
                {
                    HackArraySizeCall(array, ((ArrayHeader*)p) - 1, size, func);
                    return;
                }
            }
            func(array);
        }
    }

}