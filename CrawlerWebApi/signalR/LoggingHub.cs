using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CrawlerWebApi.signalR
{
    public class LoggingHub : Hub
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> GroupMemberships = new();

        // Method to join a specific testId group
        public async Task JoinTestGroup(string testId)
        {
            Console.WriteLine($"JoinTestGroup called for TestId: {testId}, ConnectionId: {Context.ConnectionId}");

            lock (GroupMemberships)
            {
                if (!GroupMemberships.ContainsKey(testId))
                {
                    GroupMemberships[testId] = new HashSet<string>();
                }

                // Add the connection ID to the HashSet
                GroupMemberships[testId].Add(Context.ConnectionId);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, testId);
            Console.WriteLine($"Connection {Context.ConnectionId} joined group {testId}");

            await SignalRLogger.FlushBufferedLogs(testId);
        }

        // Method to leave a specific testId group
        public async Task LeaveTestGroup(string testId)
        {
            Console.WriteLine($"Client {Context.ConnectionId} attempting to leave group {testId}");

            if (GroupMemberships.TryGetValue(testId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    GroupMemberships.TryRemove(testId, out _);
                }
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, testId);
            Console.WriteLine($"Client {Context.ConnectionId} successfully left group {testId}");
        }

        // Override OnDisconnectedAsync to handle group cleanup
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            Console.WriteLine($"Connection {Context.ConnectionId} disconnected.");

            foreach (var testId in GroupMemberships.Keys)
            {
                if (GroupMemberships[testId].Remove(Context.ConnectionId) && GroupMemberships[testId].Count == 0)
                {
                    GroupMemberships.TryRemove(testId, out _);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to send log messages to a specific testId group
        public async Task SendLogMessage(string testId, string message)
        {
            try
            {
                await Clients.Group(testId).SendAsync("ReceiveLogMessage", new { testId, message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending log message to group {testId}: {ex.Message}");
            }
        }

        public static bool IsGroupReady(string testId)
        {
            lock (GroupMemberships)
            {
                if (GroupMemberships.TryGetValue(testId, out var connections) && connections != null)
                {
                    Console.WriteLine($"[IsGroupReady] TestId: {testId}, Connection Count: {connections.Count}");
                    return connections.Count > 0;
                }

                Console.WriteLine($"[IsGroupReady] TestId: {testId} not ready (no connections)");
                return false;
            }
        }

        public Task SignalReady(string testId)
        {
            Console.WriteLine($"[SignalReady] TestId: {testId} readiness signaled by {Context.ConnectionId}");

            lock (GroupMemberships)
            {
                if (!GroupMemberships.ContainsKey(testId))
                {
                    GroupMemberships[testId] = new HashSet<string>();
                }

                GroupMemberships[testId].Add(Context.ConnectionId);
            }

            return Task.CompletedTask;
        }

    }
}
