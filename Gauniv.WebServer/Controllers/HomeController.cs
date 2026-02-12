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
using System.Diagnostics;
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Dtos;
using Gauniv.WebServer.Models;
using Gauniv.WebServer.Websocket;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<User> userManager;

        public HomeController(ApplicationDbContext db, UserManager<User> userManager)
        {
            this.db = db;
            this.userManager = userManager;
        }

        public async Task<IActionResult> Index(
            string? filterName,
            decimal? filterMinPrice,
            decimal? filterMaxPrice,
            string? filterCategory,
            bool? filterOwned)
        {
            var user = await userManager.GetUserAsync(User);
            bool isAdmin = user != null && await userManager.IsInRoleAsync(user, "Admin");

            var allGames = await db.Games
                .Include(g => g.Categories)
                .Include(g => g.PurchasedByUsers)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    Price = g.Price,
                    PayloadSize = g.PayloadSize,
                    Categories = g.Categories.Select(c => c.Name).ToList(),
                    Owned = false
                }).ToListAsync();

            if (user != null)
            {
                var ownedGameIds = user.PurchasedGames.Select(g => g.Id).ToHashSet();
                allGames.ForEach(g => g.Owned = ownedGameIds.Contains(g.Id));
            }

            if (filterOwned.HasValue)
            {
                if (user != null)
                {
                    if (filterOwned.Value)
                        allGames = allGames.Where(g => g.Owned).ToList();
                    else
                        allGames = allGames.Where(g => !g.Owned).ToList();
                }
                else
                {
                    if (filterOwned.Value)
                    {
                        allGames = new List<GameDto>();
                    }
                    else
                    {
                        allGames = allGames.Where(g => !g.Owned).ToList();
                    }
                }
            }

            if (!string.IsNullOrEmpty(filterName))
                allGames = allGames.Where(g => g.Name.Contains(filterName, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (filterMinPrice.HasValue)
                allGames = allGames.Where(g => g.Price >= filterMinPrice.Value).ToList();
            if (filterMaxPrice.HasValue)
                allGames = allGames.Where(g => g.Price <= filterMaxPrice.Value).ToList();
            if (!string.IsNullOrEmpty(filterCategory))
                allGames = allGames.Where(g => g.Categories.Contains(filterCategory)).ToList();

            var model = new HomeViewModel
            {
                Games = allGames,
                FilterName = filterName,
                FilterMinPrice = filterMinPrice,
                FilterMaxPrice = filterMaxPrice,
                FilterCategory = filterCategory,
                FilterOwned = filterOwned
            };

            if (isAdmin)
            {
                var categories = await db.Categories.Include(c => c.Games).ToListAsync();
                
                int maxSimultaneousPlayers = OnlineHub.ConnectedUsers.Values.Sum(u => u.Count);

                var maxPlayersPerGame = OnlineHub.ConnectedUsers
                    .Where(u => u.Value.CurrentGameId.HasValue)
                    .GroupBy(u => u.Value.CurrentGameId.Value)
                    .Select(g => new GamePlayerStatDto
                    {
                        GameName = categories.SelectMany(c => c.Games)
                                            .FirstOrDefault(game => game.Id == g.Key)?.Name ?? $"Jeu {g.Key}",
                        MaxPlayers = g.Count()
                    })
                    .ToList();

                model.Stats = new StatsViewModel
                {
                    TotalGames = await db.Games.CountAsync(),
                    GamesPerCategory = categories.Select(c => new CategoryStatDto
                    {
                        CategoryName = c.Name,
                        Count = c.Games.Count
                    }).ToList(),
                    AvgGamesPerUser = await db.Users.Select(u => u.PurchasedGames.Count).AverageAsync(),
                    MaxSimultaneousPlayers = maxSimultaneousPlayers,
                    MaxPlayersPerGame = maxPlayersPerGame
                };
            }

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
