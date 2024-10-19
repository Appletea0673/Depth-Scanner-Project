using System.Collections.Generic;
using UnityEngine;

namespace Appletea.Dev.PointCloud
{
    public class PointCloudRenderer : MonoBehaviour
    {
        private GameObject pointPrefab;
        private List<GameObject> pointPool;
        private int activePointCount = 0;

        // 初期化メソッド
        public void Initialize(GameObject pointPrefab, int initialPoolSize)
        {
            this.pointPrefab = pointPrefab;

            // プールの初期化
            pointPool = new List<GameObject>(initialPoolSize);
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject point = Instantiate(pointPrefab);
                point.SetActive(false); // 最初は無効化
                pointPool.Add(point);
            }
        }

        // 点群を更新するメソッド
        public void UpdatePointCloud(List<Vector3> points)
        {
            // 必要に応じてプールを拡張
            if (points.Count > pointPool.Count)
            {
                int additionalPoints = points.Count - pointPool.Count;
                for (int i = 0; i < additionalPoints; i++)
                {
                    GameObject point = Instantiate(pointPrefab);
                    point.SetActive(false);
                    pointPool.Add(point);
                }
            }

            // プールから必要な数だけオブジェクトを有効化し、位置を設定
            for (int i = 0; i < points.Count; i++)
            {
                GameObject point = pointPool[i];
                point.transform.position = points[i];
                point.SetActive(true);
            }

            // 前回の更新で有効化したオブジェクトをすべて無効化
            for (int i = points.Count; i < activePointCount; i++)
            {
                pointPool[i].SetActive(false);
            }

            // 現在の有効なオブジェクトの数を更新
            activePointCount = points.Count;
        }
    }
}