#region Licence
// Cyril Tisserand
// Projet Gauniv - WebServer
// Gauniv 2025
// 
// Licence MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the “Software”), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// Any new method must be in a different namespace than the previous ones
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions: 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. 
// The Software is provided “as is”, without warranty of any kind, express or implied,
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
using Gauniv.WebServer.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MapsterMapper;
using Mapster;
using Microsoft.EntityFrameworkCore;


[Route("api/1.0.0/games")]
[ApiController]
public class GamesController : ControllerBase
{
    private readonly ApplicationDbContext db;
    private readonly IMapper mapper;
    private readonly UserManager<User> userManager;
    private readonly string storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "GamesStorage");

    public GamesController(ApplicationDbContext db, IMapper mapper, UserManager<User> userManager)
    {
        this.db = db;
        this.mapper = mapper;
        this.userManager = userManager;

        if (!Directory.Exists(storageRoot))
            Directory.CreateDirectory(storageRoot);
    }

    [HttpGet]
    public async Task<IActionResult> GetGames(
        [FromQuery] string? name,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int[]? categoryIds,
        [FromQuery] bool? owned,
        [FromQuery] long? minSize,
        [FromQuery] long? maxSize,
        [FromQuery] int? offset,
        [FromQuery] int? limit)
    {
        var user = await userManager.GetUserAsync(User);

        if (owned == true && user == null)
            return Unauthorized();

        var query = db.Games
            .Include(g => g.Categories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(g => g.Name.Contains(name));

        if (minPrice.HasValue)
            query = query.Where(g => g.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(g => g.Price <= maxPrice.Value);

        if (minSize.HasValue)
            query = query.Where(g => g.PayloadSize >= minSize.Value);

        if (maxSize.HasValue)
            query = query.Where(g => g.PayloadSize <= maxSize.Value);

        if (categoryIds != null && categoryIds.Length > 0)
        {
            query = query.Where(g =>
                g.Categories.Count(c => categoryIds.Contains(c.Id)) == categoryIds.Length);
        }

        if (owned.HasValue)
        {
            if (owned.Value)
            {
                query = query.Where(g =>
                    g.PurchasedByUsers.Any(u => u.Id == user!.Id));
            }
            else
            {
                query = query.Where(g =>
                    g.PurchasedByUsers.All(u => u.Id != user!.Id));
            }
        }

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var games = await query
            .Select(g => new GameDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Price = g.Price,
                PayloadSize = g.PayloadSize,
                Categories = g.Categories.Select(c => c.Name).ToList(),
                Owned = user != null && g.PurchasedByUsers.Any(u => u.Id == user.Id)
            })
            .ToListAsync();

        return Ok(games);
    }

    [HttpPost("{id:int}/buy")]
    [Authorize]
    public async Task<IActionResult> Buy(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var game = await db.Games
            .Include(g => g.PurchasedByUsers)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null) return NotFound();
        if (!game.PurchasedByUsers.Any(u => u.Id == user.Id))
            game.PurchasedByUsers.Add(user);

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Add([FromForm] CreateGameDto dto)
    {
        if (dto.PayloadFile == null || dto.PayloadFile.Length == 0)
            return BadRequest("A game file must be uploaded.");

        var game = new Game
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            PayloadPath = "",
            PayloadSize = 0,
            Categories = await db.Categories
                .Where(c => dto.Categories.Contains(c.Name))
                .ToListAsync()
        };

        db.Games.Add(game);
        await db.SaveChangesAsync();

        var gameFolder = Path.Combine(storageRoot, $"game-{game.Id}");
        if (Directory.Exists(gameFolder))
            Directory.Delete(gameFolder, true);
        Directory.CreateDirectory(gameFolder);

        var filePath = Path.Combine(gameFolder, $"{game.Name}.7z");
        await using (var stream = new FileStream(filePath, FileMode.Create))
            await dto.PayloadFile.CopyToAsync(stream);

        game.PayloadPath = filePath;
        game.PayloadSize = dto.PayloadFile.Length;

        await db.SaveChangesAsync();

        return Ok(new GameDto
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            Price = game.Price,
            PayloadSize = game.PayloadSize,
            Categories = game.Categories.Select(c => c.Name).ToList(),
            Owned = false
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateGameDto dto)
    {
        var game = await db.Games
            .Include(g => g.Categories)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Name))
            game.Name = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Description))
            game.Description = dto.Description;
        if (dto.Price.HasValue)
            game.Price = dto.Price.Value;

        if (dto.PayloadFile != null && dto.PayloadFile.Length > 0)
        {

            var gameFolder = Path.Combine(storageRoot, $"game-{game.Id}");
            Directory.CreateDirectory(gameFolder);

            var filePath = Path.Combine(gameFolder, $"{game.Name}.7z");

            await using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.PayloadFile.CopyToAsync(stream);

            game.PayloadPath = filePath;
            game.PayloadSize = dto.PayloadFile.Length;
        }

        if (dto.Categories != null)
        {
            var categories = new List<Category>();
            foreach (var catName in dto.Categories)
            {
                var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == catName)
                            ?? new Category { Name = catName };
                categories.Add(category);
            }
            game.Categories = categories;
        }

        await db.SaveChangesAsync();

        return Ok(new GameDto
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            Price = game.Price,
            PayloadSize = game.PayloadSize,
            Categories = game.Categories.Select(c => c.Name).ToList(),
            Owned = false
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var game = await db.Games.FindAsync(id);
        if (game == null) return NotFound();

        var folder = Path.GetDirectoryName(game.PayloadPath);
        if (folder != null && Directory.Exists(folder))
            Directory.Delete(folder, true);

        db.Games.Remove(game);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:int}/download")]
    [Authorize]
    public async Task<IActionResult> Download(int id)
    {
        var game = await db.Games.FindAsync(id);
        if (game == null) 
            return NotFound("Game not found.");

        if (!System.IO.File.Exists(game.PayloadPath))
            return NotFound("Game file not found on server.");

        var stream = new FileStream(
            game.PayloadPath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.Read, 
            bufferSize: 81920,
            useAsync: true
        );

        return File(
            stream,
            "application/octet-stream",
            $"{game.Name}.7z",
            enableRangeProcessing: true
        );

    }

}
