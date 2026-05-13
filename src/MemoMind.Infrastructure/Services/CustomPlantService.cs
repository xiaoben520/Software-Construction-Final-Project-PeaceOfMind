using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

public class CustomPlantService : ICustomPlantService
{
    private readonly AppDbContext dbContext;

    public CustomPlantService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomPlantProfile>> GetAllAsync()
    {
        return await dbContext.CustomPlantProfiles
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<CustomPlantProfile> AddAsync(CustomPlantProfile profile)
    {
        profile.CreatedAt = DateTime.Now;
        profile.UpdatedAt = DateTime.Now;
        dbContext.CustomPlantProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile;
    }

    public async Task UpdateAsync(CustomPlantProfile profile)
    {
        profile.UpdatedAt = DateTime.Now;
        dbContext.CustomPlantProfiles.Update(profile);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var profile = await dbContext.CustomPlantProfiles.FindAsync(id);
        if (profile is null)
        {
            return;
        }

        dbContext.CustomPlantProfiles.Remove(profile);
        await dbContext.SaveChangesAsync();
    }
}
