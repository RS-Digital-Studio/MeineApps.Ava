using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service for employer management (Premium feature for multiple employers)
/// </summary>
public interface IEmployerService
{
    /// <summary>
    /// Get all employers
    /// </summary>
    Task<List<Employer>> GetEmployersAsync(bool includeInactive = false);

    /// <summary>
    /// Get default employer
    /// </summary>
    Task<Employer?> GetDefaultEmployerAsync();

    /// <summary>
    /// Save employer
    /// </summary>
    Task SaveEmployerAsync(Employer employer);

    /// <summary>
    /// Delete employer
    /// </summary>
    Task DeleteEmployerAsync(int id);

    /// <summary>
    /// Set employer as default
    /// </summary>
    Task SetDefaultEmployerAsync(int id);

    /// <summary>
    /// Get work hours per employer for a period
    /// </summary>
    Task<Dictionary<Employer, double>> GetEmployerHoursAsync(DateTime start, DateTime end);

    /// <summary>
    /// Variante, die mit bereits geladenen WorkDays arbeitet — vermeidet doppelte
    /// DB-Queries wenn der Aufrufer (z.B. StatisticsViewModel) die WorkDays sowieso schon hält.
    /// </summary>
    Task<Dictionary<Employer, double>> GetEmployerHoursAsync(IReadOnlyList<WorkDay> workDays);
}
