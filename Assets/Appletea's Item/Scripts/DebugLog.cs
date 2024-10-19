using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Appletea.Dev
{
    public class DebugLog : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI debugTextUI;  // デバッグ情報を表示するTextコンポーネント
        private Queue logMessages = new Queue();  // ログメッセージのキュー
        private string currentLog = "";  // 現在表示されているログ

        void OnEnable()
        {
            // ログメッセージをリッスンする
            Application.logMessageReceived += HandleLog;
        }

        void OnDisable()
        {
            // リッスンを停止する
            Application.logMessageReceived -= HandleLog;
        }

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            // ログをキューに追加
            logMessages.Enqueue(logString);
            if (logMessages.Count > 10)  // 表示するログの行数を制限
            {
                logMessages.Dequeue();
            }

            // キューからログメッセージを文字列として組み立てる
            currentLog = "";
            foreach (string log in logMessages)
            {
                currentLog += log + "\n";
            }
        }

        void Update()
        {
            // テキストUIにログを表示
            if (debugTextUI != null)
            {
                debugTextUI.text = currentLog;
            }
        }
    }

}