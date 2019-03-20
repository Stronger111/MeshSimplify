#if UNITY_2018_1_OR_NEWER
using Unity.Jobs;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Chaos;
using System;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
/// <summary>
/// 转换为Job使用的数据格式
/// 把所有数据转换成结构体进行计算
/// @author:DXS
/// </summary>
/// 
#region  安全的指针
public unsafe struct Pointer : IDisposable
{
    public void* _ptr;
    //private bool _disposed;
    Allocator _label;
    public void Malloc(int size, int align, Allocator label)
    {
        _ptr = UnsafeUtility.Malloc(size, align, label);
        UnsafeUtility.MemClear(_ptr, size);
        _label = label;
    }

    public void Dispose()
    {
        //if (_disposed)
        //{
        //    throw new ObjectDisposedException("hahahha");
        //}
        //_disposed = true;
        UnsafeUtility.Free(_ptr, _label);
        _ptr = null;
    }

    public T Get<T>(int idx) where T : struct
    {
        return UnsafeUtility.ReadArrayElement<T>(_ptr, idx);
    }

    public void Set<T>(int idx, T val) where T : struct
    {
        UnsafeUtility.WriteArrayElement<T>(_ptr, idx, val);
    }

    public void CopyFrom(Pointer other, int size)
    {
        UnsafeUtility.MemCpy(_ptr, other._ptr, size);
    }
}
public unsafe struct Point1
{
    NativeArray<int> bbb;
}
#endregion
/// <summary>
///顶点坍塌值最小记录成一个最小队列
/// </summary>
public unsafe struct ComputeHeapJob : IJob
{
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<TriangleStruct> triangles;
    [ReadOnly]
    public NativeArray<StructRelevanceSphere> Spheres;
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> m_aVertexPermutation;
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> m_aVertexMap;
    [NativeDisableContainerSafetyRestriction] //解除写限制
    public NativeHeap<VertexStruct> m_VertexHeap;
    //[NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> idToHeapIndex;
    public bool UseEdgeLength;
    public bool UseCurvature;
    public bool LockBorder;
    public float OriginalMeshSize;
    [NativeDisableContainerSafetyRestriction]
    public NativeList<int> tmpVerticesList;
    [NativeDisableContainerSafetyRestriction]
    public NativeList<int> tempTriangleList;
    private const float MAX_VERTEX_COLLAPSE_COST = 10000000.0f;
    public void Execute()
    {
        //提取顶部元素
        int vertexNum = m_VertexHeap.Length;
        while (vertexNum-- > 0)
        {
            VertexStruct mn = m_VertexHeap.ExtractTop();
            //Debug.Log("mn的Position为" + mn.Position.ToString() + "mn的ID" + mn.ID  + "m_VertexHeap Length" + m_VertexHeap.Length + "坍塌目标" + mn.collapse
            //    + "mnHeapIndex" + mn.HeapIndex + "mn的object" + mn.m_fObjDist);
            m_aVertexPermutation[mn.ID] = vertexNum;
            m_aVertexMap[mn.ID] = mn.collapse != -1 ? mn.collapse : -1;
            Collapse(mn, mn.collapse, Spheres);   //坍塌目标的结构如何改写
        }
    }
    /// <summary>
    /// 获取下标为Index的三角形
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    TriangleStruct GetTriangles(int index)
    {
        return triangles[index];
    }
    /// <summary>
    /// 临时缓存的三角形集合
    /// </summary>
    unsafe void Collapse(VertexStruct u, int v, NativeArray<StructRelevanceSphere> aRelevanceSpheres)
    {
        if (v == -1)   //v=9779  坍塌目标
        {
            u.Destructor();    //析构掉
            return;
        }

        int i;
        //u临近的顶点  把临近顶点添加到tmpVertices队列里面
        tmpVerticesList.Clear();
        for (i = 0; i < u.currentNeightCount; i++)   //u是一个顶点
        {
            int nb = u.pneighbors.Get<int>(i);
            if (nb != u.ID)
            {
                tmpVerticesList.Add(nb);
            }
        }
        //Debug
        tempTriangleList.Clear();
        for (i = 0; i < u.currentFaceCount; i++)
        {
            if (GetTriangles(u.pfaces.Get<int>(i)).HasVertex(v))  //获取到三角形
            {
                tempTriangleList.Add(u.pfaces.Get<int>(i));
            }
        }
        // Delete triangles on edge uv
        for (i = tempTriangleList.Length - 1; i >= 0; i--)
        {
            TriangleStruct t = GetTriangles(tempTriangleList[i]);
            //获取t的三个顶点信息
            t.Destructor(ref m_VertexHeap, ref triangles, ref u, idToHeapIndex);
            triangles[tempTriangleList[i]] = t;
        }
        // Update remaining triangles to have v instead of u
        //缓存vNew 
        VertexStruct vNewVertex = m_VertexHeap[idToHeapIndex[v]];
        for (i = u.currentFaceCount - 1; i >= 0; i--)
        {
            TriangleStruct t = GetTriangles(u.pfaces.Get<int>(i));
            t.ReplaceVertex(ref m_VertexHeap, u, ref vNewVertex, idToHeapIndex);
            triangles[u.pfaces.Get<int>(i)] = t;  //更新
        }
        //更新VNewIndex
        m_VertexHeap[idToHeapIndex[v]] = vNewVertex;
        u.Destructor();
        // Recompute the edge collapse costs for neighboring vertices
        for (i = 0; i < tmpVerticesList.Length; i++)
        {
            VertexStruct st = m_VertexHeap[idToHeapIndex[tmpVerticesList[i]]];
            ComputeEdgeCostAtVertex(ref st, aRelevanceSpheres);
            m_VertexHeap[idToHeapIndex[tmpVerticesList[i]]] = st;
            m_VertexHeap.ModifyValue(st.HeapIndex, st);
        }
    }
    void ComputeEdgeCostAtVertex(ref VertexStruct v, NativeArray<StructRelevanceSphere> aRelevanceSpheres)
    {
        if (v.currentNeightCount == 0)
        {
            v.collapse = -1;   //-1表示没有下一个坍塌目标
            v.m_fObjDist = -0.01f;
            return;
        }

        v.m_fObjDist = MAX_VERTEX_COLLAPSE_COST;
        v.collapse = -1;

        float fRelevanceBias = 0.0f;

        if (aRelevanceSpheres.Length != 0)
        {
            for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
            {
                Matrix4x4 mtxSphere = aRelevanceSpheres[nSphere].Transformation;

                Vector3 v3World = v.PositionWorld;
                Vector3 v3Local = mtxSphere.inverse.MultiplyPoint(v3World);

                if (v3Local.magnitude <= 0.5f)
                {
                    // Inside
                    fRelevanceBias = aRelevanceSpheres[nSphere].Relevance;
                }
            }
        }

        for (int i = 0; i < v.currentNeightCount; i++)
        {
            float dist = ComputeEdgeCollapseCost(v, m_VertexHeap[idToHeapIndex[v.pneighbors.Get<int>(i)]], fRelevanceBias);
            if (v.collapse == -1 || dist < v.m_fObjDist)  //-1表示没有坍塌目标的存在
            {
                v.collapse = v.pneighbors.Get<int>(i);
                v.m_fObjDist = dist;
            }
        }
    }

    float ComputeEdgeCollapseCost(VertexStruct u, VertexStruct v, float fRelevanceBias)
    {
        bool bUseEdgeLength = UseEdgeLength;
        bool bUseCurvature = UseCurvature;
        bool bLockBorder = LockBorder;

        int i;
        float fEdgeLength = bUseEdgeLength ? (Vector3.Magnitude(v.Position - u.Position) / OriginalMeshSize) : 1.0f;
        float fCurvature = 0.001f;
        List<TriangleStruct> sides = new List<TriangleStruct>();
        for (i = 0; i < u.currentFaceCount; i++)
        {
            TriangleStruct ut = GetTriangles(u.pfaces.Get<int>(i));
            if (HasVertex(ut, v.ID))
            {
                sides.Add(ut);
            }
        }
        if (bUseCurvature)
        {
            for (i = 0; i < u.currentFaceCount; i++)
            {
                float fMinCurv = 1.0f;

                for (int j = 0; j < sides.Count; j++)
                {
                    float dotprod = Vector3.Dot(GetTriangles(u.pfaces.Get<int>(i)).Normal, sides[j].Normal);
                    fMinCurv = Mathf.Min(fMinCurv, (1.0f - dotprod) / 2.0f);
                }
                fCurvature = Mathf.Max(fCurvature, fMinCurv);

            }
        }
        bool isBorder = u.IsBorder(triangles);
        if (isBorder && sides.Count > 1)
        {
            fCurvature = 1.0f;
        }

        if (bLockBorder && isBorder)
        {
            fCurvature = MAX_VERTEX_COLLAPSE_COST;
        }

        fCurvature += fRelevanceBias;
        return fEdgeLength * fCurvature;
    }

    static bool HasVertex(TriangleStruct t, int v)
    {
        return IndexOf(t, v) >= 0;  //>=0
    }
    static int IndexOf(TriangleStruct t, int v)
    {
        for (int i = 0; i < 3; i++)
        {
            if (t.vertexs[i] == v)
            {
                return i;
            }
        }
        return -1;
    }
}
public class ComputeHeap
{
    private JobHandle handle;
    private ComputeHeapJob computerHeapJob;
    private int[] m_aVertexPermutation;
    private int[] m_VertexHeap;
    private bool isHandle = false;
    public unsafe void Compute(List<Vertex> vertices, Mesh m_OriginalMesh, TriangleList[] triangleArray, RelevanceSphere[] aRelevanceSpheres, float[] costs, int[] collapses, int[] m_aVertexPermutation, int[] m_VertexHeap, bool bUseEdgeLength, bool bUseCurvature, bool bLockBorder, float fOriginalMeshSize)
    {
        computerHeapJob = new ComputeHeapJob();
        this.m_aVertexPermutation = m_aVertexPermutation;
        this.m_VertexHeap = m_VertexHeap;
        computerHeapJob.UseEdgeLength = bUseEdgeLength;
        computerHeapJob.UseCurvature = bUseCurvature;
        computerHeapJob.LockBorder = bLockBorder;
        computerHeapJob.OriginalMeshSize = fOriginalMeshSize;
        computerHeapJob.m_aVertexPermutation = new NativeArray<int>(m_aVertexPermutation, Allocator.TempJob);
        computerHeapJob.m_aVertexMap = new NativeArray<int>(m_VertexHeap, Allocator.TempJob);
        computerHeapJob.idToHeapIndex = new NativeArray<int>(vertices.Count, Allocator.TempJob);
        computerHeapJob.tempTriangleList = new NativeList<int>(Allocator.TempJob);
        computerHeapJob.tmpVerticesList = new NativeList<int>(Allocator.TempJob);
        NativeHeap<VertexStruct> minNativeHeap = NativeHeap<VertexStruct>.CreateMinHeap(vertices.Count, Allocator.TempJob);
        int intSize = UnsafeUtility.SizeOf<int>();
        int intAlignment = UnsafeUtility.AlignOf<int>();
        //------------------------顶点数据---------------------------------
        for (int i = 0; i < vertices.Count; i++)
        {
            Vertex v = vertices[i];
            VertexStruct sv = new VertexStruct()
            {
                ID = v.m_nID,
                m_fObjDist = costs[i],
                Position = v.m_v3Position,
                collapse = collapses[i] == -1 ? -1 : vertices[collapses[i]].m_nID,
                isBorder = v.IsBorder() ? 1 : 0,
            };
            sv.NewVertexNeighbors(v.m_listNeighbors.Count, intSize, intAlignment, Allocator.TempJob);
            sv.NewFaceTriangle(v.m_listFaces.Count, intSize, intAlignment, Allocator.TempJob);
            sv.idToHeapIndex = computerHeapJob.idToHeapIndex;
            for (int j = 0; j < v.m_listNeighbors.Count; j++)
            {
                sv.AddNeighborVertex(v.m_listNeighbors[j].m_nID);
            }
            for (int j = 0; j < v.m_listFaces.Count; j++)
            {
                sv.AddFaceTriangle(v.m_listFaces[j].Index);
            }
            minNativeHeap.Insert(sv);
        }
        //------------------------顶点数据结束-----------------------------
        //------------------------三角形数据-------------------------------
        List<TriangleStruct> structTrangle = new List<TriangleStruct>();
        for (int n = 0; n < triangleArray.Length; n++)
        {
            List<Triangle> triangles = triangleArray[n].m_listTriangles;
            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                TriangleStruct st = new TriangleStruct()
                {
                    Index = t.Index,
                    Indices = (int*)UnsafeUtility.Malloc(t.Indices.Length * intAlignment, intAlignment, Allocator.TempJob),
                    Normal = t.Normal,
                    faceIndex = (int*)UnsafeUtility.Malloc(t.FaceIndex.Length * intSize, intAlignment, Allocator.TempJob),
                    vertexs = t.Vertices.Length == 0 ? null : (int*)UnsafeUtility.Malloc(t.Vertices.Length * intSize, intAlignment, Allocator.TempJob),
                };

                for (int j = 0; j < t.Indices.Length; j++)
                {
                    st.Indices[j] = t.Indices[j];
                }
                for (int j = 0; j < t.FaceIndex.Length; j++)
                {
                    st.faceIndex[j] = t.FaceIndex[j];
                }
                for (int j = 0; j < t.Vertices.Length; j++)
                {
                    st.vertexs[j] = t.Vertices[j].m_nID;
                }
                structTrangle.Add(st);
            }
        }
        //------------------------三角形数据结束
        computerHeapJob.Spheres = new NativeArray<StructRelevanceSphere>(aRelevanceSpheres.Length, Allocator.TempJob);
        for (int i = 0; i < aRelevanceSpheres.Length; i++)
        {
            RelevanceSphere rs = aRelevanceSpheres[i];
            StructRelevanceSphere srs = new StructRelevanceSphere()
            {
                Transformation = Matrix4x4.TRS(rs.m_v3Position, rs.m_q4Rotation, rs.m_v3Scale),
                Relevance = rs.m_fRelevance,
            };
            computerHeapJob.Spheres[i] = srs;
        }
        computerHeapJob.triangles = new NativeArray<TriangleStruct>(structTrangle.ToArray(), Allocator.TempJob);

        computerHeapJob.m_VertexHeap = minNativeHeap;
        handle = computerHeapJob.Schedule();
        //Debug.Log(" computerHeapJob.m_VertexHeap.Length" + computerHeapJob.m_VertexHeap.Length);
    }
    public bool GetJobHandleIsComplete()
    {
        return handle.IsCompleted;
    }
    public bool IsHandle()
    {
        return isHandle;
    }
    public void JobComputeComplete()
    {
        if (!isHandle)
        {
            handle.Complete();
            //复制数据
            computerHeapJob.m_aVertexPermutation.CopyTo(m_aVertexPermutation);
            computerHeapJob.m_aVertexMap.CopyTo(m_VertexHeap);

            computerHeapJob.m_VertexHeap.Dispose();
            computerHeapJob.Spheres.Dispose();
            computerHeapJob.m_aVertexPermutation.Dispose();
            computerHeapJob.m_aVertexMap.Dispose();
            computerHeapJob.triangles.Dispose();
            computerHeapJob.idToHeapIndex.Dispose();
            computerHeapJob.tempTriangleList.Dispose();
            computerHeapJob.tmpVerticesList.Dispose();
            isHandle = true;
        }
    }
}
#endif

