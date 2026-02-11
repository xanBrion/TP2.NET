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
using Gauniv.WebServer.Websocket;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using System.Text;

namespace Gauniv.WebServer.Services
{
    public class SetupService : IHostedService
    {
        private ApplicationDbContext? applicationDbContext;
        private readonly IServiceProvider serviceProvider;
        private Task? task;

        public SetupService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                applicationDbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                var userManager = scope.ServiceProvider.GetService<UserManager<User>>();
                var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole>>();

                if (applicationDbContext is null || userManager is null || roleManager is null)
                    throw new Exception("Required services are null");

                var adminRole = new IdentityRole("Admin");
                var userRole = new IdentityRole("User");
                roleManager.CreateAsync(adminRole).Wait();
                roleManager.CreateAsync(userRole).Wait();

                var testUser = new User
                {
                    UserName = "test@test.com",
                    Email = "test@test.com",
                    EmailConfirmed = true,
                    FirstName = "Test",
                    LastName = "User"
                };
                userManager.CreateAsync(testUser, "password").Wait();
                userManager.AddToRoleAsync(testUser, "User").Wait();

                var adminUser = new User
                {
                    UserName = "admin@test.com",
                    Email = "admin@test.com",
                    EmailConfirmed = true,
                    FirstName = "Test",
                    LastName = "Admin"
                };
                userManager.CreateAsync(adminUser, "password").Wait();
                userManager.AddToRoleAsync(adminUser, "Admin").Wait();

                var cat1 = new Category { Name = "Aventure" };
                var cat2 = new Category { Name = "Stratégie" };
                applicationDbContext.Categories.AddRange(cat1, cat2);
                applicationDbContext.SaveChanges();

                var categories = applicationDbContext.Categories.ToList();

                var testGame = new Game
                {
                    Name = "JeuTest",
                    Description = "Un jeu de test",
                    Price = 9.99M,
                    Categories = categories
                };
                applicationDbContext.Games.Add(testGame);
                applicationDbContext.SaveChanges();

                testUser.PurchasedGames.Add(testGame);
                userManager.UpdateAsync(testUser).Wait();

                return Task.CompletedTask;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
