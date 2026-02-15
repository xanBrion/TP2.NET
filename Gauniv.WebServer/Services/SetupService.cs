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
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Services
{
    public class SetupService : IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        private ApplicationDbContext? dbContext;

        private readonly string storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "GamesStorage");

        public SetupService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            if (!Directory.Exists(storageRoot))
                Directory.CreateDirectory(storageRoot);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();

            dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole>>();

            if (dbContext == null || userManager == null || roleManager == null)
                throw new Exception("Required services are null");

            var roles = new[] { "Admin", "User" };
            foreach (var roleName in roles)
            {
                if (!roleManager.Roles.Any(r => r.Name == roleName))
                    roleManager.CreateAsync(new IdentityRole(roleName)).Wait();
            }

            var adminUser = CreateUser(userManager, "admin@test.com", "Admin", "User", "password", "Admin");
            var testUser = CreateUser(userManager, "test@test.com", "Test", "User", "password", "User");

            var categoryNames = new[] { "Aventure", "Stratégie", "RPG", "Simulation", "Puzzle" };
            var categories = new List<Category>();
            foreach (var name in categoryNames)
            {
                var cat = dbContext.Categories.FirstOrDefault(c => c.Name == name)
                          ?? new Category { Name = name };
                if (cat.Id == 0)
                    dbContext.Categories.Add(cat);
                categories.Add(cat);
            }
            dbContext.SaveChanges();

            var testGames = new[]
            {
                new { Name = "Epic Adventure", Description = "Un RPG épique plein de quêtes.", Price = 19.99M, Categories = new [] { "Aventure", "RPG" }, PayloadFile = @"C:\WORKSPACE\TP2.NET\Gauniv.WebServer\GamesStorage\test\payload.exe" },
                new { Name = "Puzzle Mania", Description = "Résolvez des puzzles complexes.", Price = 4.99M, Categories = new [] { "Puzzle" }, PayloadFile = @"C:\WORKSPACE\TP2.NET\Gauniv.WebServer\GamesStorage\test\payload.exe" },
                new { Name = "Strategy King", Description = "Dominez vos ennemis grâce à votre stratégie.", Price = 14.99M, Categories = new [] { "Stratégie" }, PayloadFile = @"C:\WORKSPACE\TP2.NET\Gauniv.WebServer\GamesStorage\test\payload.exe" }
            };

            foreach (var g in testGames)
            {
                var game = new Game
                {
                    Name = g.Name,
                    Description = g.Description,
                    Price = g.Price,
                    Categories = dbContext.Categories.Where(c => g.Categories.Contains(c.Name)).ToList()
                };

                dbContext.Games.Add(game);
                dbContext.SaveChanges();

                var gameFolder = Path.Combine(storageRoot, $"game-{game.Id}");
                if (Directory.Exists(gameFolder))
                    Directory.Delete(gameFolder, true);
                Directory.CreateDirectory(gameFolder);

                var destPath = Path.Combine(gameFolder, "payload.exe");
                File.Copy(g.PayloadFile, destPath, true);

                game.PayloadPath = destPath;
                game.PayloadSize = new FileInfo(destPath).Length;

                dbContext.SaveChanges();

                testUser.PurchasedGames.Add(game);
            }

            userManager.UpdateAsync(testUser).Wait();

            return Task.CompletedTask;
        }

        private User CreateUser(UserManager<User> userManager, string email, string firstName, string lastName, string password, string role)
        {
            var user = userManager.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName
                };
                userManager.CreateAsync(user, password).Wait();
            }
            if (!userManager.IsInRoleAsync(user, role).Result)
                userManager.AddToRoleAsync(user, role).Wait();
            return user;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
