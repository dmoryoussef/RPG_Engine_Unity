using System;
using System.Collections.Generic;
using UnityEngine;

namespace Logging
{
    public enum GameMessageChannel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public sealed class GameMessage
    {
        public DateTime TimeUtc { get; }
        public GameMessageChannel Channel { get; }
        public string SystemTag { get; }
        public string Action { get; }
        public string Result { get; }
        public string Message { get; }
        public GameObject SourceObject { get; }
        public Component SourceComponent { get; }

        public GameMessage(
            GameMessageChannel channel,
            string systemTag,
            string action,
            string result,
            string message,
            Component source)
        {
            TimeUtc = DateTime.UtcNow;
            Channel = channel;
            SystemTag = systemTag;
            Action = action;
            Result = result;
            Message = message;
            SourceComponent = source;
            SourceObject = source != null ? source.gameObject : null;
        }

        public override string ToString()
        {
            var goName = SourceObject != null ? SourceObject.name : "<no GO>";
            var compName = SourceComponent != null ? SourceComponent.GetType().Name : "<no component>";
            var msgPart = string.IsNullOrEmpty(Message) ? string.Empty : $" :: {Message}";
            return $"[{SystemTag}] {goName} ({compName}) {Action} => {Result}{msgPart}";
        }
    }

    /// <summary>
    /// Static logging utility. Mirrors Unity's Log/LogWarning/LogError API
    /// but adds structured game context + message history.
    /// </summary>
    public static class GameLog
    {
        public static event Action<GameMessage> MessageLogged;

        private static readonly List<GameMessage> _messages = new List<GameMessage>();
        public static IReadOnlyList<GameMessage> Messages => _messages;

        /// <summary>Maximum messages stored in memory (ring buffer).</summary>
        public static int MaxMessages { get; set; } = 200;

        /// <summary>Echo to Unity's console.</summary>
        public static bool EchoToUnityConsole { get; set; } = true;

        private static void AddMessage(GameMessage message, bool? echoOverride = null)
        {
            if (message == null) return;

            _messages.Add(message);
            if (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);

            var echo = echoOverride ?? EchoToUnityConsole;
            if (echo)
            {
                var ctx = (UnityEngine.Object)(message.SourceComponent ?? (UnityEngine.Object)message.SourceObject);

                switch (message.Channel)
                {
                    case GameMessageChannel.Warning:
                        Debug.LogWarning(message.ToString(), ctx);
                        break;
                    case GameMessageChannel.Error:
                        Debug.LogError(message.ToString(), ctx);
                        break;
                    default:
                        Debug.Log(message.ToString(), ctx);
                        break;
                }
            }

            MessageLogged?.Invoke(message);
        }

        // --------------------------------------------------------------------
        // PUBLIC API – Unity-style Log / LogWarning / LogError
        // --------------------------------------------------------------------

        /// <summary>
        /// Log a structured debug/info message.
        /// </summary>
        public static void Log(
            Component owner,
            string system,
            string action,
            string result,
            string message = null,
            bool? echoOverride = null)
        {
            if (owner == null) return;

            var msg = new GameMessage(
                GameMessageChannel.Debug,
                system,
                action,
                result,
                message,
                owner);

            AddMessage(msg, echoOverride);
        }

        /// <summary>
        /// Log a structured info message (semantically "user-facing success/info").
        /// </summary>
        public static void LogInfo(
            Component owner,
            string system,
            string action,
            string result,
            string message = null,
            bool? echoOverride = null)
        {
            if (owner == null) return;

            var msg = new GameMessage(
                GameMessageChannel.Info,
                system,
                action,
                result,
                message,
                owner);

            AddMessage(msg, echoOverride);
        }

        /// <summary>
        /// Log a structured warning.
        /// </summary>
        public static void LogWarning(
            Component owner,
            string system,
            string action,
            string message,
            bool? echoOverride = null)
        {
            if (owner == null) return;

            var msg = new GameMessage(
                GameMessageChannel.Warning,
                system,
                action,
                "Warning",
                message,
                owner);

            AddMessage(msg, echoOverride);
        }

        /// <summary>
        /// Log a structured error.
        /// </summary>
        public static void LogError(
            Component owner,
            string system,
            string action,
            string message,
            bool? echoOverride = null)
        {
            if (owner == null) return;

            var msg = new GameMessage(
                GameMessageChannel.Error,
                system,
                action,
                "Error",
                message,
                owner);

            AddMessage(msg, echoOverride);
        }
    }
}
