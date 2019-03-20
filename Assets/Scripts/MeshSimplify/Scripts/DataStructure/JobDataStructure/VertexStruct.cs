using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
/// <summary>
/// 模型顶点数据
/// </summary>
/// 
public unsafe struct VertexStruct : IHeapNode, IComparable<VertexStruct>
{
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> idToHeapIndex;
    /// <summary>
    /// 在堆中的索引
    /// </summary>
    private int _HeapIndex;
    /// <summary>
    /// 堆中Index
    /// </summary>
    public int HeapIndex
    {
        get
        {
            return _HeapIndex;
        }
        set
        {
            _HeapIndex = value;
            idToHeapIndex[ID] = _HeapIndex;
        }
    }
    public int CompareTo(VertexStruct other)
    {
        return this.m_fObjDist > other.m_fObjDist ? 1 : this.m_fObjDist < other.m_fObjDist ? -1 : 0;
    }
    public float m_fObjDist; // Cached cost of collapsing edge
    public int collapse; // Candidate vertex for collapse
    public Vector3 Position;
    public Vector3 PositionWorld;
    public int ID; // Place of vertex in original list
    public Pointer pneighbors;
    public int neightborsCount;
    public int currentNeightCount;
    public Pointer pfaces;
    public int faceCount;
    public int currentFaceCount;
    public int isBorder;
    private int intSize;
    private int intAlignment;
    private Allocator vertexAllocatorLabel;
    private Allocator triangleAllocatorLabel;

    public void NewVertexNeighbors(int count, int intSize, int intAlignment, Allocator allocatorLable)
    {
        neightborsCount = count == 0 ? 1 : count;
        this.intSize = intSize;
        this.intAlignment = intAlignment;
        this.vertexAllocatorLabel = allocatorLable;
        currentNeightCount = 0;
        pneighbors.Malloc(neightborsCount * intSize, intAlignment, allocatorLable);
    }

    public void NewFaceTriangle(int count, int intSize, int intAlignment, Allocator allocatorLable)
    {
        faceCount = count == 0 ? 1 : count;
        currentFaceCount = 0;
        this.triangleAllocatorLabel = allocatorLable;
        pfaces.Malloc(faceCount * intSize, intAlignment, allocatorLable);
    }

    /// <summary>
    /// 析构顶点
    /// </summary>
    public void Destructor()
    {
        for (int i = 0; i < currentNeightCount; i++)
        {
            if (pneighbors.Get<int>(i) == ID)
            {
                RemoveAtVertex(i);
            }
        }
    }

    public void RemoveAtVertex(int index)
    {
        for (int i = index; i < currentNeightCount - 1; i++)
        {
            pneighbors.Set<int>(i, pneighbors.Get<int>(i + 1));
        }
        currentNeightCount--;
    }
    /// <summary>
    /// 移除三角形
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAtTriangele(int index)
    {
        for (int i = index; i < currentFaceCount - 1; i++)
        {
            pfaces.Set<int>(i, pfaces.Get<int>(i + 1));
        }
        currentFaceCount--;
    }
    public void RemoveAtVertex(VertexStruct t)
    {
        for (int i = 0; i < currentNeightCount; i++)
        {
            if (pneighbors.Get<int>(i) == t.ID)
            {
                RemoveAtVertex(i);
            }
        }
    }
    public void AddFaceTriangle(int t)
    {
        if (currentFaceCount >= faceCount)
        {
            int newFaceCount = faceCount * 2;
            Pointer newData = new Pointer();
            newData.Malloc(newFaceCount * intSize, intAlignment, triangleAllocatorLabel);
            newData.CopyFrom(pfaces, currentFaceCount * intSize);
            pfaces.Dispose();
            pfaces = newData;
            faceCount = newFaceCount;
        }
        pfaces.Set<int>(currentFaceCount++, t);
    }
    public void AddNeighborVertex(int t)
    {
        if (currentNeightCount >= neightborsCount)
        {
            //开始扩充长度为原来得两倍
            int newNeightborsCount = neightborsCount * 2;    //+Add 先这样修改一下
            Pointer newData = new Pointer();
            newData.Malloc(newNeightborsCount * intSize, intAlignment, vertexAllocatorLabel);
            newData.CopyFrom(pneighbors, currentNeightCount * intSize);
            pneighbors.Dispose();
            pneighbors = newData;
            neightborsCount = newNeightborsCount;
        }
        //UnsafeUtility.MemCpy((VertexStruct*)pneighbors._ptr + currentNeightCount++, UnsafeUtility.AddressOf<VertexStruct>(ref t), vertexSize);
        pneighbors.Set<int>(currentNeightCount++, t);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public bool Contains(int t)
    {
        for (int i = 0; i < currentNeightCount; i++)
        {
            if (pneighbors.Get<int>(i) == t)
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 移除相邻的顶点
    /// </summary>
    /// <param name="n"></param>
    public void RemoveIfNonNeighbor(int ID, NativeArray<TriangleStruct> triangeleArray)
    {
        int idx = IndexOf(ID);
        if (idx < 0)
        {
            return;
        }
        for (int i = 0; i < currentFaceCount; i++)
        {

            if (triangeleArray[pfaces.Get<int>(i)].HasVertex(ID))
            {
                return;
            }
        }
        RemoveAtVertex(idx);
    }

    public bool Equal(VertexStruct other)
    {
        return this.ID == other.ID;
    }
    int IndexOf(int ID)
    {
        for (int i = 0; i < currentNeightCount; i++)
        {
            if (pneighbors.Get<int>(i) == ID)
            {
                return i;
            }
        }
        return -1;
    }
    public bool IsBorder(NativeArray<TriangleStruct> triangles)
    {
        int i, j;

        for (i = 0; i < currentNeightCount; i++)
        {
            int nCount = 0;

            for (j = 0; j < currentFaceCount; j++)
            {
                TriangleStruct st = triangles[pfaces.Get<int>(j)];
                if (st.HasVertex(pneighbors.Get<int>(i)))
                {
                    nCount++;
                }
            }
            if (nCount == 1)
            {
                return true;
            }
        }
        return false;
    }
}