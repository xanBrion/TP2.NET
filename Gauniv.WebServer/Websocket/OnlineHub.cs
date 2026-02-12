#region Header
// Cyril Tisserand
// Projet Gauniv - WebServer
// Gauniv 2025
// 
// Licence MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// Any new method must be in a different namespace than the previous ones
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions: 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. 
// The Software is provided "as is", without warranty of any kind, express or implied,
// including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.
// Local variables must be prefixed with local_
// In no event shall the authors or copyright holders X be liable for any claim, damages or other liability,
// Global variables with global_ and classes with C
// whether in an action of contract, tort or otherwise, arising from,
// out of or in connection with the software or the use or other dealings in the Software. 
// 
// Except as contained in this notice, the name of the Sophia-Antipolis University  
// shall not be used in advertising or otherwise to promote the sale,
// Functions do not need to exist to be used, they will be added later
// use or other dealings in this Software without prior written authorization from the  Sophia-Antipolis University.
// 
// Please respect the team's standards for any future contribution
#endregion
using Gauniv.WebServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

public class OnlineStatus
{
    public User User { get; set; } = null!;
    public int Count { get; set; }
    public int? CurrentGameId { get; set; }
}

namespace Gauniv.WebServer.Websocket
{
    public class OnlineHub : Hub
    {
        public static Dictionary<string, OnlineStatus> ConnectedUsers = new();

        private readonly UserManager<User> userManager;

        public OnlineHub(UserManager<User> userManager)
        {
            this.userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            var user = await userManager.GetUserAsync(Context.User);
            if (user == null)
                return;

            lock (ConnectedUsers)
            {
                if (!ConnectedUsers.ContainsKey(user.Id))
                {
                    ConnectedUsers[user.Id] = new OnlineStatus
                    {
                        User = user,
                        Count = 1
                    };

                    user.Status = UserStatus.Online;
                }
                else
                {
                    ConnectedUsers[user.Id].Count++;
                }
            }

            await userManager.UpdateAsync(user);

            await Clients.All.SendAsync(
                "UserStatusChanged",
                user.Id,
                user.Status
            );

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await userManager.GetUserAsync(Context.User);
            if (user == null)
                return;

            lock (ConnectedUsers)
            {
                if (ConnectedUsers.ContainsKey(user.Id))
                {
                    ConnectedUsers[user.Id].Count--;

                    if (ConnectedUsers[user.Id].Count <= 0)
                    {
                        ConnectedUsers.Remove(user.Id);

                        user.Status = UserStatus.Offline;
                    }
                }
            }

            await userManager.UpdateAsync(user);

            await Clients.All.SendAsync(
                "UserStatusChanged",
                user.Id,
                user.Status
            );

            await base.OnDisconnectedAsync(exception);
        }
        public async Task SetInGame(int gameId)
        {
            var principal = Context.User;
            if (principal == null) return;

            var user = await userManager.GetUserAsync(principal);
            if (user == null) return;

            lock(ConnectedUsers)
            {
                if (ConnectedUsers.TryGetValue(user.Id, out var status))
                {
                    status.CurrentGameId = gameId;
                    user.Status = UserStatus.InGame;
                }
            }

            await userManager.UpdateAsync(user);
            await Clients.All.SendAsync("UserStatusChanged", user.Id, user.Status, gameId);
        }

        public async Task SetOnline()
        {
            var user = await userManager.GetUserAsync(Context.User);
            if (user == null)
                return;

            user.Status = UserStatus.Online;
            await userManager.UpdateAsync(user);

            await Clients.All.SendAsync(
                "UserStatusChanged",
                user.Id,
                user.Status
            );
        }
    }
}
