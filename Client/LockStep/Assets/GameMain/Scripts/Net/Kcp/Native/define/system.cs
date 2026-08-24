using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#pragma warning disable CS1591

// ReSharper disable ALL

namespace kcp
{
    internal static unsafe partial class KCP
    {
        private static void* malloc(nuint size)
        {
            return (void*)Marshal.AllocHGlobal((nint)size);
        }

        private static void free(void* memory)
        {
            Marshal.FreeHGlobal((nint)memory);
        }

        private static void memcpy(void* dst, void* src, nuint size)
        {
            Buffer.MemoryCopy(src, dst, (long)size, (long)size);
        }

        private static void memset(void* dst, byte val, nuint size)
        {
            byte* p = (byte*)dst;
            for (nuint i = 0; i < size; i++)
            {
                p[i] = val;
            }
        }

        [Conditional("DEBUG")]
        private static void assert(bool condition) => Debug.Assert(condition);

        private static void abort() => Environment.Exit(-1);
    }
}