using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface ICustomPlantService
{
    Task<IReadOnlyList<CustomPlantProfile>> GetAllAsync();
    Task<CustomPlantProfile> AddAsync(CustomPlantProfile profile);
    Task UpdateAsync(CustomPlantProfile profile);
    Task DeleteAsync(int id);
}
