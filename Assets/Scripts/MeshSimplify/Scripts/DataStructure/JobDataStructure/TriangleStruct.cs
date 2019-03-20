using Unity.Collections;
using UnityEngine;
/// <summary>
/// 模型三角形数据
/// </summary>
public unsafe struct TriangleStruct
{
    /// <summary>
    /// 顶点索引顺序
    /// </summary>
    public int* Indices;
    /// <summary>
    /// 三角形法线
    /// </summary>
    public Vector3 Normal;
    /// <summary>
    /// 在三角形集合里面的索引
    /// </summary>
    public int Index;
    /// <summary>
    ///描述顶点的紧邻三角形
    /// </summary>
    public int* vertexs;
    /// <summary>
    /// 紧邻的三角形索引
    /// </summary>
    public int* faceIndex;
    /// <summary>
    /// 析构方法
    /// </summary>
    public void Destructor(ref NativeHeap<VertexStruct> vertexHeap, ref NativeArray<TriangleStruct> triangleArray, ref VertexStruct vold, NativeArray<int> idToHeapIndex)
    {
        //动态数组
        int i;
        for (i = 0; i < 3; i++)
        {
            if (vertexs[i] != -1)
            {
                VertexStruct v;

                if (vertexs[i] == vold.ID)
                {
                    v = vold;
                }
                else
                {
                    v = vertexHeap[idToHeapIndex[vertexs[i]]];
                }
                Pointer ptr = v.pfaces;
                int triangleIndices = ptr.Get<int>(v.currentFaceCount - 1);
                ptr.Set<int>(faceIndex[i], triangleIndices);
                TriangleStruct t = triangleArray[triangleIndices];
                t.faceIndex[t.IndexOf(v.ID)] = faceIndex[i];
                //修改完重新赋值
                v.RemoveAtTriangele(v.currentFaceCount - 1);     //直接修改里面的内容了
                triangleArray[triangleIndices] = t;
                v.pfaces = ptr;
                if (vertexs[i] == vold.ID)
                {
                    vold = v;
                }
                else
                {
                    vertexHeap[idToHeapIndex[vertexs[i]]] = v;
                }
            }
        }
        for (i = 0; i < 3; i++)
        {
            int i2 = (i + 1) % 3;
            if (vertexs[i] == -1 || vertexs[i2] == -1) continue;
            VertexStruct v;
            VertexStruct v2;
            int indexI = -1;
            int indexI2 = -1;
            if (vertexs[i] == vold.ID)
            {
                v = vold;
            }
            else
            {
                indexI = 1;
                v = vertexHeap[idToHeapIndex[vertexs[i]]];
            }
            if (vertexs[i2] == vold.ID)
            {
                v2 = vold;
            }
            else
            {
                indexI2 = 1;
                v2 = vertexHeap[idToHeapIndex[vertexs[i2]]];
            }
            v.RemoveIfNonNeighbor(vertexs[i2], triangleArray);
            if (indexI != -1)
                vertexHeap[idToHeapIndex[vertexs[i]]] = v;
            else
                vold = v;
            v2.RemoveIfNonNeighbor(vertexs[i], triangleArray);
            if (indexI2 != -1)
                vertexHeap[idToHeapIndex[vertexs[i2]]] = v2;
            else
                vold = v2;
        }
    }
    /// <summary>
    /// 查找Vertex在全局堆中的位置
    /// </summary>
    /// <param name="vertexHeap">Heap堆</param>
    /// <param name="id">顶点ID</param>
    /// <returns></returns>
    int FindIndexVertex(NativeHeap<VertexStruct> vertexHeap, int id)
    {
        for (int i = 0; i < vertexHeap.Length; i++)
        {
            VertexStruct vertex = vertexHeap[i];
            if (vertex.ID == id)
            {
                return i;
            }
        }
        return -1;
    }
    public int IndexOf(int v)
    {
        for (int i = 0; i < 3; i++)
        {
            if (v == vertexs[i])
            {
                return i;
            }
        }
        return -1;
    }
    public bool HasVertex(int v)
    {
        return IndexOf(v) >= 0; //(v == m_aVertices[0] || v == m_aVertices[1] || v == m_aVertices[2]);
    }
    public void ReplaceVertex(ref NativeHeap<VertexStruct> vertexHeap, VertexStruct vold, ref VertexStruct vNew, NativeArray<int> idToHeapIndex)
    {
        int idx;
        for (idx = 0; idx < 3; idx++)
        {
            if (vold.ID == vertexs[idx])
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i == idx)
                    {
                        continue;
                    }
                    VertexStruct v = vertexHeap[idToHeapIndex[vertexs[i]]];
                    v.RemoveAtVertex(vold);
                    if (!v.Contains(vNew.ID))
                    {
                        v.AddNeighborVertex(vNew.ID);

                    }
                    if (!vNew.Contains(vertexs[i]))
                    {
                        vNew.AddNeighborVertex(vertexs[i]);
                    }
                    vertexHeap[idToHeapIndex[vertexs[i]]] = v;
                }
                vertexs[idx] = vNew.ID;
                break;
            }
        }
        faceIndex[idx] = vNew.currentFaceCount;
        vNew.AddFaceTriangle(Index);
        ComputeNormal(vertexHeap, vold, idToHeapIndex);
    }
    public void ComputeNormal(NativeHeap<VertexStruct> vertexHeap, VertexStruct vertex, NativeArray<int> idToHeapIndex)
    {
        Vector3 v0 = Vector3.zero;
        Vector3 v1 = Vector3.zero;
        Vector3 v2 = Vector3.zero;
        if (vertexs[0] == vertex.ID)
        {
            v0 = vertex.Position;
        }
        else
        {
            v0 = vertexHeap[idToHeapIndex[vertexs[0]]].Position;
        }
        if (vertexs[1] == vertex.ID)
        {
            v1 = vertex.Position;
        }
        else
        {
            v1 = vertexHeap[idToHeapIndex[vertexs[1]]].Position;
        }
        if (vertexs[2] == vertex.ID)
        {
            v2 = vertex.Position;
        }
        else
        {
            v2 = vertexHeap[idToHeapIndex[vertexs[2]]].Position;
        }
        Normal = Vector3.Cross((v1 - v0), (v2 - v1));

        if (Normal.magnitude == 0.0f) return;

        Normal = Normal / Normal.magnitude;
    }
}