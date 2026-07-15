using UnityEngine;
using UnityEngine.UI; // For regular Text
using TMPro; // Uncomment if using TextMeshPro
using System.Collections.Generic;

public class DebugLogToUI : MonoBehaviour
{
    public TextMeshPro logText; // For regular UI Text
    // public TextMeshProUGUI logText; // Uncomment if using TextMeshPro

    private Queue<string> logMessagesQueue = new Queue<string>();
    private int maxMessages = 5;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Add new message to the queue
        logMessagesQueue.Enqueue(logString);

        // If the queue exceeds the maximum number of messages, remove the oldest one
        if (logMessagesQueue.Count > maxMessages)
        {
            logMessagesQueue.Dequeue();
        }

        // Update the displayed text with the messages in the queue
        logText.text = string.Join("\n", logMessagesQueue.ToArray());
    }
}
