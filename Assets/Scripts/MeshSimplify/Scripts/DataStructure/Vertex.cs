﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chaos
{

    /// <summary>
    /// Stores topology information and edge collapsing information.
    /// </summary>
    public class Vertex : IHeapNode, IComparable<Vertex>
    {
        public int HeapIndex { get; set; }

        public int CompareTo(Vertex other)
        {
            return this.m_fObjDist > other.m_fObjDist ? 1 : this.m_fObjDist < other.m_fObjDist ? -1 : 0;
        }

        public Vector3 m_v3Position;
        public Vector3 m_v3PositionWorld;
        public int m_nID; // Place of vertex in original list
        public List<Vertex> m_listNeighbors; // Adjacent vertices
        public List<Triangle> m_listFaces; // Adjacent triangles
        public float m_fObjDist; // Cached cost of collapsing edge
        public Vertex m_collapse; // Candidate vertex for collapse
        public bool m_bRuntimeCollapsed;
        private Vector2 _uv;
        private Vector3 _normal;
        public Vector2 UV
        {
            get { return _uv; }
        }
        public Vector3 Normal
        {
            get { return _normal; }
        }
        public Vertex(Vector3 v, Vector3 v3World, int nID,Vector2 uv,Vector3 normal)
        {
            m_v3Position = v;
            m_v3PositionWorld = v3World;
            this.m_nID = nID;
            _uv = uv;
            _normal = normal;
            m_listNeighbors = new List<Vertex>();
            m_listFaces = new List<Triangle>();
        }

        public void Destructor()
        {
            for (int i = 0, imax = m_listNeighbors.Count; i < imax; i++)
            {
                m_listNeighbors[i].m_listNeighbors.Remove(this);
            }
            //while (m_listNeighbors.Count > 0)
            //{
            //    m_listNeighbors[m_listNeighbors.Count - 1].m_listNeighbors.Remove(this);

            //    if (m_listNeighbors.Count > 0)
            //    {
            //        m_listNeighbors.RemoveAt(m_listNeighbors.Count - 1);
            //    }
            //}
        }

        public void RemoveIfNonNeighbor(Vertex n)
        {
            int idx = m_listNeighbors.IndexOf(n);
            if (idx < 0)
            {
                return;
            }

            for (int i = 0; i < m_listFaces.Count; i++)
            {
                if (m_listFaces[i].HasVertex(n))
                {
                    return;
                }
            }

            m_listNeighbors.RemoveAt(idx);
        }

        public bool IsBorder()
        {
            int i, j;

            for (i = 0; i < m_listNeighbors.Count; i++)
            {
                int nCount = 0;

                for (j = 0; j < m_listFaces.Count; j++)
                {
                    if (m_listFaces[j].HasVertex(m_listNeighbors[i]))
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
    };
}