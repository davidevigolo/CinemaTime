using BotPollo.Attributes;
using BotPollo.Logging;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    public delegate void TimeOutCallback();
    public delegate void DialogueClosedCallback();
    class Dialogue
    {
        public static Dictionary<int, Dialogue> DialogueManager { get; private set; }
        public static IdManager IdGenerator { get; private set; }
        public string Username { get; private set; }
        public IUser User { get; private set; }
        public ISocketMessageChannel Channel { get; private set; }
        public int TimeOut { get; set; }
        public event TimeOutCallback Timeout;
        public event DialogueClosedCallback DialogueClosed;
        private TaskCompletionSource<SocketMessage> UserResponse;
        public RestUserMessage LastMessage { get; private set; }
        private List<IMessage> CachedMessages;
        private SemaphoreSlim Padlock;
        public int Id { get; private set; }
        public Dialogue(IUser user, ISocketMessageChannel channel, int timeOut)
        {
            User = user;
            Channel = channel;
            TimeOut = timeOut;
            UserResponse = new TaskCompletionSource<SocketMessage>();
            DialogueManager = new Dictionary<int, Dialogue>();
            IdGenerator = new IdManager(1420);
            Padlock = new SemaphoreSlim(1);

            Program.DiscordClient.MessageReceived += MessageReceivedHandler;

            CachedMessages = new List<IMessage>();
            DialogueManager.Add(IdGenerator.GetId(), this);
        }

        ~Dialogue() {
            Close();
        }

        private Task MessageReceivedHandler(SocketMessage message)
        {
            if (Channel == message.Channel && User == message.Author)
                UserResponse.SetResult(message);
            return Task.CompletedTask;
        }

        public async Task<SocketMessage> GetUserReplyAsync(bool deleteCachedMessagesAfter = false, bool deleteCacheIfTimedOut = true, bool cacheReply = false)
        {
            var x = Task.WaitAny(new[] { UserResponse.Task }, TimeOut);
            if (x != -1)
            {
                var result = UserResponse.Task.Result;
                if (cacheReply)
                    CachedMessages.Add(result);
                if (deleteCachedMessagesAfter)
                    await ClearMessageCacheAsync();
                UserResponse = new TaskCompletionSource<SocketMessage>(); //Resetting reply event source
                return result;
            }
            else
            {
                if (Timeout != null)
                    Timeout();
                if (deleteCacheIfTimedOut)
                    await ClearMessageCacheAsync();
                UserResponse = new TaskCompletionSource<SocketMessage>(); //Resetting reply event source
                return null;
            }
        }
        public async Task<IMessage> ReplyAsync(string message, bool modifyLastMessage = true, bool cacheMessage = true, bool clearCacheAfterSending = false)
        {
            if (!modifyLastMessage)
                LastMessage = await Channel.SendMessageAsync(message);
            else
                await LastMessage.ModifyAsync(x => x.Content = message);

            if (cacheMessage && !CachedMessages.Contains(LastMessage)) //SISTEMARE QUI
                CachedMessages.Add(LastMessage);
            if (clearCacheAfterSending)
                await ClearMessageCacheAsync();
            return LastMessage;
        }

        public void Close()
        {
            if (DialogueClosed != null)
                DialogueClosed();
            Program.DiscordClient.MessageReceived -= MessageReceivedHandler;
            UserResponse.TrySetCanceled();
        }
        public async Task ClearMessageCacheAsync()
        {
            Logger.Console_Log("Deleting cached dialogue messages...", LogLevel.Trace);
            Logger.Console_Log("Awaiting for thread being released...", LogLevel.Trace);
            await Padlock.WaitAsync();
            Logger.Console_Log("Thread released...", LogLevel.Trace);
            try
            {
                foreach (IMessage m in CachedMessages.ToList()) { await m.DeleteSafeAsync(CachedMessages); }
                CachedMessages.Clear();
            }
            catch (Discord.Net.HttpException ex)
            {
                Logger.Console_Log(ex.Message, LogLevel.Error);
            }
            CachedMessages.Clear();
            Padlock.Release();
            Logger.Console_Log("Dialogue cache cleared!", LogLevel.Trace);
            DialogueManager.Remove(Id);
            Logger.Console_Log($"Dialogue {Id} closed", LogLevel.Trace);
        }
    }
}
