using Gauniv.WebServer.Data;
using Gauniv.WebServer.Dtos;
using Gauniv.WebServer.Models;
using Gauniv.WebServer.Websocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext db;

        public AdminController(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await db.Categories
                                     .Include(c => c.Games)
                                     .ToListAsync();

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

            var local_games = await db.Games
                .Include(g => g.Categories)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    Price = g.Price,
                    PayloadSize = g.PayloadSize,
                    Categories = g.Categories.Select(c => c.Name).ToList(),
                    Owned = false
                })
                .ToListAsync();

            var AvailableCategories = await db.Categories
                .Select(c => c.Name)
                .ToListAsync();

            var model = new AdminViewModel
            {
                Stats = new StatsViewModel
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
                },
                Games = local_games,
                AvailableCategories = AvailableCategories
            };


            return View(model);
        }
    }
}
