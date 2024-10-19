using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Appletea.Dev.PointCloud
{
    public class PlyLib
    {
        public static string ExportToPly(string directoryPath, List<Vector3> points)
        {

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Add Timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Output_{timestamp}.ply";
            string filePath = Path.Combine(directoryPath, fileName);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Header
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine($"element vertex {points.Count}");
                writer.WriteLine("property float x");
                writer.WriteLine("property float y");
                writer.WriteLine("property float z");
                writer.WriteLine("end_header");

                // Vertex Data
                foreach (var point in points)
                {
                    writer.WriteLine($"{point.x} {point.y} {point.z}");
                }
            }

            return filePath;
        }
    }
}