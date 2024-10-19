using System;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Appletea.Dev.PointCloud
{
    public class ChunkManager
    {
        private Dictionary<(int, int, int), LimitedQueue<Vector3>> chunks;
        private int chunkSize;
        private int maxPointsPerChunk;

        public ChunkManager(int chunkSize, int maxPointsPerChunk)
        {
            this.chunkSize = chunkSize;
            this.maxPointsPerChunk = maxPointsPerChunk;
            chunks = new Dictionary<(int, int, int), LimitedQueue<Vector3>>();
        }

        public void AddPoint(Vector3 point)
        {
            var chunkIndex = (
                Mathf.FloorToInt(point.x / chunkSize),
                Mathf.FloorToInt(point.y / chunkSize),
                Mathf.FloorToInt(point.z / chunkSize));

            if (!chunks.TryGetValue(chunkIndex, out var chunk))
            {
                chunk = new LimitedQueue<Vector3> { Limit = maxPointsPerChunk };
                chunks[chunkIndex] = chunk;
            }

            chunk.Enqueue(point);
        }

        private Vector3 ChunkIndexToVector3((int, int, int) chunkIndex, float chunkSize)
        {
            // ChunkÇÃíÜêSì_ÇåvéZÇ∑ÇÈ
            float x = chunkIndex.Item1 * chunkSize + chunkSize / 2.0f;
            float y = chunkIndex.Item2 * chunkSize + chunkSize / 2.0f;
            float z = chunkIndex.Item3 * chunkSize + chunkSize / 2.0f;

            return new Vector3(x, y, z);
        }

        public List<Vector3> GetPointsInRadius(Vector3 center, float radius, int maxChunkCount)
        {
            List<(float distance, LimitedQueue<Vector3> points)> chunksWithDistance = new List<(float, LimitedQueue<Vector3>)>();

            foreach (var kvp in chunks)
            {
                Vector3 chunkPos = ChunkIndexToVector3(kvp.Key, chunkSize);
                float distance = Vector3.Distance(center, chunkPos);
                if (distance < radius)
                {
                    chunksWithDistance.Add((distance, kvp.Value));
                }
            }

            chunksWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));

            if (chunksWithDistance.Count > maxChunkCount)
            {
                chunksWithDistance.RemoveRange(maxChunkCount, chunksWithDistance.Count - maxChunkCount);
            }

            List<Vector3> result = new List<Vector3>();
            foreach (var (_, points) in chunksWithDistance)
            {
                result.AddRange(points);
            }

            return result;
        }


        public List<Vector3> GetAllPoints()
        {
            List<Vector3> allPoints = new List<Vector3>();

            foreach (var chunk in chunks.Values)
            {
                allPoints.AddRange(chunk);
            }

            return allPoints;
        }

        
    }

    public static class ListExtensions
    {
        private static System.Random rng = new System.Random();

        // Fisher-Yates Shuffle
        public static void Shuffle<T>(this List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1); // 0 <= k <= n
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    public class LimitedQueue<T> : Queue<T>
    {
        public int Limit { get; set; }
        public new void Enqueue(T item)
        {
            while (Count >= Limit)
            {
                Dequeue();
            }
            base.Enqueue(item);
        }
    }
}