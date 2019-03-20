using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
/// <summary>
/// Native Heap Data
/// </summary>
unsafe struct NativeHeapData
{
    /// <summary>
    /// 指向数组的动态指针
    /// </summary>
    public void* mBuffer;
    /// <summary>
    /// 堆的长度
    /// </summary>
    public int m_length;
    /// <summary>
    /// 最大容量
    /// </summary>
    public int capacity;
}
/// <summary>
/// Native堆
/// </summary>
[StructLayout(LayoutKind.Sequential)] //???这里面不一定是Length在堆中Length只是下面得一个属性
[DebuggerDisplay("Length={Length}")]
[NativeContainer]  //自定义本机容器
public unsafe struct NativeHeap<T> : IDisposable where T : struct, IHeapNode, IComparable<T>    //UnSafeUtils
{
    [NativeDisableUnsafePtrRestriction]
    NativeHeapData* m_HeapData;     //定义一个buffer进行双重缓9存访问
    internal AtomicSafetyHandle m_Safety;
    [NativeSetClassTypeToNullOnSchedule]
    internal DisposeSentinel m_DisposeSentinel;
    /// <summary>
    /// 分配内存方式
    /// </summary>
    internal Allocator m_AllocatorLabel;
    public bool isSmaller;
    //public delegate bool Compare(T a, T b);
    //public Compare _compareFuc;
    /// <summary>
    /// 构造堆
    /// </summary>
    /// <param name="array"></param>
    /// <param name="allocator"></param>
    public NativeHeap(int capacity, Allocator allocator, bool isSmaller)
    {
        if (allocator <= Allocator.None)
            throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be>=0");
        m_HeapData = (NativeHeapData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NativeHeapData>(), UnsafeUtility.AlignOf<NativeHeapData>(), allocator);
        m_AllocatorLabel = allocator;
        //_compareFuc = compareFunc;
        this.isSmaller = isSmaller;
        capacity = Math.Max(1, capacity);
        var totalSize = UnsafeUtility.SizeOf<T>() * capacity;
        m_HeapData->mBuffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
        m_HeapData->m_length = 0;
        m_HeapData->capacity = capacity;
        //用于检测内存泄漏,创建一个标志
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
    }
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="array"></param>
    /// <param name="allocator"></param>
    public NativeHeap(T[] array, Allocator allocator, bool isSmaller)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        m_HeapData = (NativeHeapData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NativeHeapData>(), UnsafeUtility.AlignOf<NativeHeapData>(), allocator);
        m_AllocatorLabel = allocator;
        //_compareFuc = compareFunc;
        this.isSmaller = isSmaller;
        var totalSize = UnsafeUtility.SizeOf<T>() * (long)array.Length;
        m_HeapData->mBuffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
        m_HeapData->m_length = array.Length;
        m_HeapData->capacity = array.Length;
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
        Copy(array, this);
        for (int i = m_HeapData->m_length - 1 / 2; i >= 0; i--)
        {
            Heapify(i);
        }
    }
    /// <summary>
    /// 拷贝数组元素
    /// </summary>
    /// <param name="src">原数组</param>
    /// <param name="dst">目标Native堆</param>
    public static void Copy(T[] src, NativeHeap<T> dst)
    {
        //检查是否可以读取手柄。如果已经销毁或作业当前正在写入数据，则引发异常
        AtomicSafetyHandle.CheckReadAndThrow(dst.m_Safety);
        if (src.Length != dst.Length)
            throw new ArgumentException("source and destination length must be the same");
        Copy(src, 0, dst, 0, src.Length);
    }
    public static void Copy(T[] src, int srcIndex, NativeHeap<T> dst, int dstIndex, int length)
    {
        AtomicSafetyHandle.CheckReadAndThrow(dst.m_Safety);
        if (src == null)
            throw new ArgumentNullException(nameof(src));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero");
        if (srcIndex < 0 || srcIndex > src.Length || (srcIndex == src.Length && src.Length > 0))
            throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source array.");

        if (dstIndex < 0 || dstIndex > dst.Length || (dstIndex == dst.Length && dst.Length > 0))
            throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray.");

        if (srcIndex + length > src.Length)
            throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source array.", nameof(length));

        if (dstIndex + length > dst.Length)
            throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", nameof(length));
        //防止原数组内存回收
        var handle = GCHandle.Alloc(src, GCHandleType.Pinned);
        var addr = handle.AddrOfPinnedObject();
        UnsafeUtility.MemCpy((byte*)dst.m_HeapData->mBuffer + dstIndex * UnsafeUtility.SizeOf<T>(),
              (byte*)addr + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
        handle.Free();
    }
    public int Length => m_HeapData->m_length;
    public void CopyTo(T[] array)
    {
        Copy(this, array);
    }
    void Copy(NativeHeap<T> src, T[] dst)
    {
        AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);

        if (src.Length != dst.Length)
            throw new ArgumentException("source and destination length must be the same");

        Copy(src, 0, dst, 0, src.Length);
    }
    public static void Copy(NativeHeap<T> src, int srcIndex, T[] dst, int dstIndex, int length)
    {
        AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);

        if (dst == null)
            throw new ArgumentNullException(nameof(dst));

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");

        if (srcIndex < 0 || srcIndex > src.Length || (srcIndex == src.Length && src.Length > 0))
            throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray.");

        if (dstIndex < 0 || dstIndex > dst.Length || (dstIndex == dst.Length && dst.Length > 0))
            throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination array.");

        if (srcIndex + length > src.Length)
            throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", nameof(length));

        if (dstIndex + length > dst.Length)
            throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination array.", nameof(length));

        var handle = GCHandle.Alloc(dst, GCHandleType.Pinned);
        var addr = handle.AddrOfPinnedObject();

        UnsafeUtility.MemCpy(
            (byte*)addr + dstIndex * UnsafeUtility.SizeOf<T>(),
            (byte*)src.m_HeapData->mBuffer + srcIndex * UnsafeUtility.SizeOf<T>(),
            length * UnsafeUtility.SizeOf<T>());

        handle.Free();
    }
    /// <summary>
    /// 插入元素
    /// </summary>
    /// <param name="value"></param>
    public void Insert(T value)
    {
        if (m_HeapData->m_length >= m_HeapData->capacity)
            Capacity = m_HeapData->m_length + m_HeapData->capacity * 2;
        m_HeapData->m_length++;
        int index = m_HeapData->m_length - 1;
        value.HeapIndex = index;
        UnsafeUtility.WriteArrayElement(m_HeapData->mBuffer, index, value);
        //+Add 原来是传的参数有问题
        ModifyValue(index, value);
    }
    public int Capacity
    {
        get
        {
            if (m_HeapData == null)
                throw new ArgumentNullException();
            return m_HeapData->capacity;
        }
        set
        {
            if (m_HeapData->capacity == value)
                return;
            void* newData = UnsafeUtility.Malloc(value * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_AllocatorLabel);
            UnsafeUtility.MemCpy(newData, m_HeapData->mBuffer, m_HeapData->m_length * UnsafeUtility.SizeOf<T>());
            UnsafeUtility.Free(m_HeapData->mBuffer, m_AllocatorLabel);
            m_HeapData->mBuffer = newData;
            m_HeapData->capacity = value;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <param name="val"></param>
    public void ModifyValue(int i, T val)
    {
        val.HeapIndex = i;
        UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, i, val);
        T heapValue = Heapify(i);
        if (i != heapValue.HeapIndex)
        {
            return;
        }
        //T compareTVal = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, i);
        //if (Compare(compareTVal, val, isSmaller))
        //{
        //    val.HeapIndex = i;
        //    UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, i, val);
        //    Heapify(i);
        //    return;
        //}
        val.HeapIndex = i;
        UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, i, val);
        int p = Parent(i);
        while (i > 0 && Compare(UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, i), UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, p), isSmaller))
        {
            Swap(i, p);
            i = p;
            p = Parent(i);
        }
    }

    T Heapify(int i)
    {
        T value = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, i);
        int l = Left(i);
        int r = Right(i);
        int m = i;
        int size = m_HeapData->m_length;
        if (l < size && Compare(UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, l), UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, i), isSmaller))
        {
            m = l;
        }
        if (r < size && Compare(UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, r), UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, m), isSmaller))
        {
            m = r;
        }
        if (m != i)
        {
            Swap(i, m);
            value = Heapify(m);
        }
        return value;
    }
    /// <summary>
    /// 交换i和j元素
    /// </summary>
    /// <param name="array"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    void Swap(int i, int j)
    {
        T t = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, i);
        t.HeapIndex = j;
        T tempSwap = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, j);
        tempSwap.HeapIndex = i;
        UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, i, tempSwap);
        UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, j, t);
    }
    static int Parent(int idx)
    {
        return (idx - 1) / 2;
    }
    static int Left(int idx)
    {
        return 2 * idx + 1;
    }

    static int Right(int idx)
    {
        return 2 * idx + 2;
    }
    /// <summary>
    /// 小根堆创建
    /// </summary>
    public static NativeHeap<T> CreateMinHeap(int capacity, Allocator allocator)
    {
        //初始化堆
        return new NativeHeap<T>(capacity, allocator, true);
    }
    public static NativeHeap<T> CreateMaxHeap(int capacity, Allocator allocator)
    {
        //初始化堆
        return new NativeHeap<T>(capacity, allocator, false);
    }
    public static NativeHeap<T> CreateMaxHeap(T[] array, Allocator allocator)
    {
        return new NativeHeap<T>(array, allocator, false);
    }
    public static NativeHeap<T> CreateMinHeap(T[] array, Allocator allocator)
    {
        return new NativeHeap<T>(array, allocator, true);
    }
    /// <summary>
    /// 优先队列元素个数
    /// </summary>
    /// <returns></returns>
    public int Size()
    {
        return m_HeapData->capacity;
    }
    static bool larger(T a, T b)
    {
        return a.CompareTo(b) > 0;
    }
    static bool smaller(T a, T b)
    {
        return a.CompareTo(b) < 0;
    }
    static bool Compare(T a, T b, bool isSmaller)
    {
        if (isSmaller)
            return a.CompareTo(b) < 0;
        else
            return a.CompareTo(b) > 0;
    }
    public T ExtractTop()
    {
        if (m_HeapData->m_length <= 0)
        {
            throw new Exception("Heap underflow");
        }
        T top = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, 0);
        int last = m_HeapData->m_length - 1;
        T lastElement = UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, m_HeapData->m_length - 1);
        lastElement.HeapIndex = 0;
        UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, 0, lastElement);
        var newLength = m_HeapData->m_length - 1;
        m_HeapData->m_length = newLength;
        Heapify(0);
        return top;
    }
    public T this[int index]
    {
        get
        {
            return UnsafeUtility.ReadArrayElement<T>(m_HeapData->mBuffer, index);
        }
        set
        {
            UnsafeUtility.WriteArrayElement<T>(m_HeapData->mBuffer, index, value);
        }
    }
    #region 注释掉
    /// <summary>
    /// 开辟内存空间
    /// </summary>
    /// <param name="length"></param>
    /// <param name="allocator"></param>
    /// <param name="array"></param>
    //static void Allocate(int length, Allocator allocator, out NativeHeap<T> array)
    //{
    //    //定位T的大小并开辟长度为length大小的空间
    //    //allocator type
    //    if (allocator <= Allocator.None)
    //        throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
    //    if (length < 0)
    //        throw new ArgumentOutOfRangeException(nameof(length), "length must be>=0");
    //    //开辟
    //    array.m_Buffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);

    //    DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1);
    //}
    #endregion
    /// <summary>
    /// 释放
    /// </summary>
    [WriteAccessRequired]
    public void Dispose()
    {
        if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
            throw new InvalidOperationException("The NativeHeap can not be Disposed because it was not allocated with a valid allocator.");
        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
        UnsafeUtility.Free(m_HeapData->mBuffer, m_AllocatorLabel);
        UnsafeUtility.Free(m_HeapData, m_AllocatorLabel);
        m_HeapData->capacity = 0;
        m_HeapData->m_length = 0;
        m_HeapData->mBuffer = null;
        m_HeapData = null;
    }
}
