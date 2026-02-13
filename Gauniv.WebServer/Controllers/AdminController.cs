using Gauniv.WebServer.Data;
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

        // GET: /Admin/Index
        public async Task<IActionResult> Index()
        {
            // Récupération des catégories avec leurs jeux
            var categories = await db.Categories
                                     .Include(c => c.Games)
                                     .ToListAsync();

            // Calcul du nombre maximum de joueurs connectés simultanément
            int maxSimultaneousPlayers = OnlineHub.ConnectedUsers.Values.Sum(u => u.Count);

            // Nombre maximum de joueurs par jeu
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

            // Remplissage du ViewModel
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
                }
            };

            return View(model);
        }
    }
}
