using FinanzRechner.Helpers;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;

namespace FinanzRechner.Services;

/// <summary>
/// Berechnet Finanz-Score, Monatsvergleich und Prognosen.
/// Reine Analyse-Logik, keine eigene Persistenz.
/// </summary>
public sealed class FinancialAnalysisService : IFinancialAnalysisService
{
    private readonly IExpenseService _expenseService;
    private readonly IAccountService _accountService;
    private readonly IDebtService _debtService;
    private readonly ISavingsGoalService _savingsGoalService;
    private readonly ILocalizationService _localizationService;

    public FinancialAnalysisService(
        IExpenseService expenseService,
        IAccountService accountService,
        IDebtService debtService,
        ISavingsGoalService savingsGoalService,
        ILocalizationService localizationService)
    {
        _expenseService = expenseService;
        _accountService = accountService;
        _debtService = debtService;
        _savingsGoalService = savingsGoalService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Gebündeltes Laden: Alle Daten einmal abfragen, dann Score + Forecast + NetWorth berechnen.
    /// Spart ~4 redundante Service-Aufrufe gegenüber einzelnem Aufruf von Score + Forecast + NetWorth.
    /// CancellationToken cancelt das await, sodass schnelle Tab-Wechsel keine veralteten Ergebnisse
    /// in die UI schreiben.
    /// </summary>
    public async Task<FinancialInsightsBundle> GetAllInsightsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var prevMonth = GetPreviousMonth(today);

        // Alle Daten parallel laden (jeder Aufruf nur 1x)
        var currentMonthTask = _expenseService.GetMonthSummaryAsync(today.Year, today.Month);
        var previousMonthTask = _expenseService.GetMonthSummaryAsync(prevMonth.Year, prevMonth.Month);
        var budgetsTask = _expenseService.GetAllBudgetStatusAsync();
        var debtsTask = _debtService.GetAllDebtsAsync();
        var goalsTask = _savingsGoalService.GetAllGoalsAsync();
        var recurringTask = _expenseService.GetAllRecurringTransactionsAsync();
        var monthExpensesTask = _expenseService.GetExpensesByMonthAsync(today.Year, today.Month);
        var netWorthTask = _accountService.GetNetWorthAsync();
        var totalDebtTask = _debtService.GetTotalDebtAsync();

        await Task.WhenAll(
            currentMonthTask, previousMonthTask, budgetsTask,
            debtsTask, goalsTask, recurringTask,
            monthExpensesTask, netWorthTask, totalDebtTask)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var currentMonth = currentMonthTask.Result;
        var previousMonth = previousMonthTask.Result;
        var budgets = budgetsTask.Result;
        var debts = debtsTask.Result;
        var goals = goalsTask.Result;
        var recurring = recurringTask.Result;
        var monthExpenses = monthExpensesTask.Result;

        // Score berechnen (mit bereits geladenen Daten)
        var score = CalculateScore(currentMonth, previousMonth, budgets, debts, goals, recurring);

        // Forecast berechnen (mit bereits geladenen Monatsdaten)
        var forecast = CalculateForecast(today, monthExpenses, budgets);

        // Nettovermögen
        var netWorth = netWorthTask.Result - totalDebtTask.Result;

        return new FinancialInsightsBundle(score, forecast, netWorth);
    }

    public async Task<FinancialScore> CalculateScoreAsync()
    {
        var today = DateTime.Today;
        var prevMonth = GetPreviousMonth(today);

        var currentMonth = await _expenseService.GetMonthSummaryAsync(today.Year, today.Month);
        var previousMonth = await _expenseService.GetMonthSummaryAsync(prevMonth.Year, prevMonth.Month);
        var budgets = await _expenseService.GetAllBudgetStatusAsync();
        var debts = await _debtService.GetAllDebtsAsync();
        var goals = await _savingsGoalService.GetAllGoalsAsync();
        var recurring = await _expenseService.GetAllRecurringTransactionsAsync();

        return CalculateScore(currentMonth, previousMonth, budgets, debts, goals, recurring);
    }

    public async Task<MonthComparison> GetMonthComparisonAsync(int year, int month)
    {
        var current = await _expenseService.GetMonthSummaryAsync(year, month);
        var prev = GetPreviousMonth(new DateTime(year, month, 1));
        var previous = await _expenseService.GetMonthSummaryAsync(prev.Year, prev.Month);

        var expenseChange = previous.TotalExpenses > 0m
            ? ((current.TotalExpenses - previous.TotalExpenses) / previous.TotalExpenses) * 100m : 0m;
        var incomeChange = previous.TotalIncome > 0m
            ? ((current.TotalIncome - previous.TotalIncome) / previous.TotalIncome) * 100m : 0m;
        var balanceChange = current.Balance - previous.Balance;

        var allCategories = current.ByCategory.Keys
            .Union(previous.ByCategory.Keys)
            .Distinct();

        var categoryChanges = allCategories.Select(cat =>
        {
            var currentAmount = current.ByCategory.GetValueOrDefault(cat, 0m);
            var previousAmount = previous.ByCategory.GetValueOrDefault(cat, 0m);
            var name = CategoryLocalizationHelper.GetLocalizedName(cat, _localizationService);
            return new CategoryChange(cat, null, name, currentAmount, previousAmount);
        })
        .Where(c => c.CurrentAmount > 0m || c.PreviousAmount > 0m)
        .OrderByDescending(c => Math.Abs(c.ChangeAmount))
        .ToList();

        return new MonthComparison(
            current, previous,
            expenseChange, incomeChange, balanceChange,
            categoryChanges);
    }

    public async Task<FinancialForecast> GetForecastAsync()
    {
        var today = DateTime.Today;
        var monthExpenses = await _expenseService.GetExpensesByMonthAsync(today.Year, today.Month);
        var budgets = await _expenseService.GetAllBudgetStatusAsync();
        return CalculateForecast(today, monthExpenses, budgets);
    }

    public async Task<decimal> CalculateNetWorthAsync()
    {
        var accountNetWorth = await _accountService.GetNetWorthAsync();
        var totalDebt = await _debtService.GetTotalDebtAsync();
        return accountNetWorth - totalDebt;
    }

    #region Private Berechnungs-Methoden (arbeiten auf bereits geladenen Daten)

    private FinancialScore CalculateScore(
        MonthSummary currentMonth, MonthSummary previousMonth,
        IReadOnlyList<BudgetStatus> budgets,
        IReadOnlyList<DebtEntry> debts,
        IReadOnlyList<SavingsGoal> goals,
        IReadOnlyList<RecurringTransaction> recurring)
    {
        var factors = new List<ScoreFactor>();
        var tips = new List<string>();

        // Faktor 1: Sparquote (max 25 Punkte)
        var savingsRate = currentMonth.TotalIncome > 0m
            ? (currentMonth.Balance / currentMonth.TotalIncome) * 100m : 0m;
        var savingsPoints = savingsRate switch
        {
            >= 30m => 25, >= 20m => 20, >= 10m => 15, >= 5m => 10, >= 0m => 5, _ => 0
        };
        factors.Add(new ScoreFactor(T("ScoreSavingsRate") ?? "Sparquote", $"{savingsRate:F0}%", savingsPoints, 25));
        if (savingsRate < 20m)
            tips.Add(T("TipIncreaseSavings") ?? "Versuche mindestens 20% deines Einkommens zu sparen.");

        // Faktor 2: Budget-Einhaltung (max 25 Punkte)
        var budgetPoints = 25;
        if (budgets.Count > 0)
        {
            var exceededCount = budgets.Count(b => b.AlertLevel == BudgetAlertLevel.Exceeded);
            var warningCount = budgets.Count(b => b.AlertLevel == BudgetAlertLevel.Warning);
            budgetPoints = Math.Max(0, 25 - (exceededCount * 8) - (warningCount * 3));
            if (exceededCount > 0)
                tips.Add(string.Format(T("TipBudgetExceeded") ?? "{0} Budget(s) überschritten.", exceededCount));
        }
        else
        {
            budgetPoints = 10;
            tips.Add(T("TipSetBudgets") ?? "Erstelle Budgets um deine Ausgaben besser zu kontrollieren.");
        }
        factors.Add(new ScoreFactor(
            T("ScoreBudgetCompliance") ?? "Budget-Einhaltung",
            $"{budgets.Count(b => b.AlertLevel == BudgetAlertLevel.Safe)}/{budgets.Count}",
            budgetPoints, 25));

        // Faktor 3: Schulden-Situation (max 25 Punkte)
        var activeDebts = debts.Where(d => d.IsActive).ToList();
        var debtPoints = 25;
        if (activeDebts.Count > 0)
        {
            var totalDebt = activeDebts.Sum(d => d.RemainingAmount);
            var monthlyIncome = currentMonth.TotalIncome > 0m ? currentMonth.TotalIncome : 1m;
            var debtToIncomeRatio = totalDebt / monthlyIncome;
            debtPoints = debtToIncomeRatio switch
            {
                <= 1m => 20, <= 3m => 15, <= 6m => 10, <= 12m => 5, _ => 0
            };
            if (debtToIncomeRatio > 3m)
                tips.Add(T("TipReduceDebt") ?? "Dein Schulden-Einkommen-Verhältnis ist hoch. Priorisiere die Tilgung.");
        }
        factors.Add(new ScoreFactor(
            T("ScoreDebtSituation") ?? "Schulden-Situation",
            activeDebts.Count == 0 ? (T("ScoreDebtFree") ?? "Schuldenfrei") : $"{activeDebts.Count} aktiv",
            debtPoints, 25));

        // Faktor 4: Regelmäßigkeit & Ziele (max 25 Punkte)
        var regularityPoints = 0;
        if (currentMonth.TotalIncome > 0m) regularityPoints += 8;
        var activeGoals = goals.Where(g => !g.IsCompleted).ToList();
        if (activeGoals.Count > 0) regularityPoints += 7;
        if (previousMonth.TotalExpenses > 0m && currentMonth.TotalExpenses <= previousMonth.TotalExpenses)
            regularityPoints += 5;
        else if (previousMonth.TotalExpenses > 0m)
            regularityPoints += 2;
        if (recurring.Count > 0) regularityPoints += 5;

        factors.Add(new ScoreFactor(
            T("ScoreRegularity") ?? "Regelmäßigkeit & Ziele",
            $"{activeGoals.Count} {T("ScoreActiveGoals") ?? "Ziele"}",
            regularityPoints, 25));

        if (activeGoals.Count == 0)
            tips.Add(T("TipSetGoals") ?? "Setze dir Sparziele um motiviert zu bleiben.");

        var totalScore = factors.Sum(f => f.Points);
        var (grade, colorHex) = FinancialScore.GetGradeFromScore(totalScore);

        int? trend = null;
        if (previousMonth.TotalIncome > 0m)
        {
            var prevSavingsRate = (previousMonth.Balance / previousMonth.TotalIncome) * 100m;
            trend = (int)(savingsRate - prevSavingsRate);
        }

        return new FinancialScore
        {
            Score = totalScore, Grade = grade, GradeColorHex = colorHex,
            Factors = factors, Tips = tips, TrendFromLastMonth = trend
        };
    }

    private static FinancialForecast CalculateForecast(
        DateTime today,
        IReadOnlyList<Expense> monthExpenses,
        IReadOnlyList<BudgetStatus> budgets)
    {
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var daysPassed = today.Day;
        var daysRemaining = daysInMonth - daysPassed;

        var totalExpensesSoFar = monthExpenses.Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);
        var totalIncomeSoFar = monthExpenses.Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);

        var avgDailyExpense = daysPassed > 0 ? totalExpensesSoFar / daysPassed : 0m;
        var projectedExpenses = avgDailyExpense * daysInMonth;

        decimal? dailyBudgetRemaining = null;
        if (budgets.Count > 0 && daysRemaining > 0)
        {
            var totalBudgetRemaining = budgets.Sum(b => Math.Max(0m, b.Remaining));
            dailyBudgetRemaining = totalBudgetRemaining / daysRemaining;
        }

        // Trend-Daten — Single-Pass über alle Monatsausgaben (vorher O(N×D), jetzt O(N+D)).
        // Index 0 ungenutzt, 1..31 entsprechen Monatstagen.
        var byDay = new decimal[32];
        foreach (var e in monthExpenses)
        {
            if (e.Type == TransactionType.Expense && e.Date.Day is >= 1 and <= 31)
                byDay[e.Date.Day] += e.Amount;
        }

        var trend = new List<(int Day, decimal CumulativeExpenses)>(daysPassed);
        var cumulative = 0m;
        for (var day = 1; day <= daysPassed; day++)
        {
            cumulative += byDay[day];
            trend.Add((day, cumulative));
        }

        var forecastLine = new List<(int Day, decimal ProjectedCumulative)>(Math.Max(0, daysInMonth - daysPassed));
        var projectedCumulative = cumulative;
        for (var day = daysPassed + 1; day <= daysInMonth; day++)
        {
            projectedCumulative += avgDailyExpense;
            forecastLine.Add((day, projectedCumulative));
        }

        return new FinancialForecast
        {
            ProjectedEndOfMonthBalance = totalIncomeSoFar - projectedExpenses,
            ProjectedMonthlyExpenses = projectedExpenses,
            ProjectedMonthlyIncome = totalIncomeSoFar,
            AverageDailyExpense = avgDailyExpense,
            RemainingDaysInMonth = daysRemaining,
            DailyBudgetRemaining = dailyBudgetRemaining,
            ExpenseTrend = trend,
            ForecastLine = forecastLine
        };
    }

    #endregion

    private static DateTime GetPreviousMonth(DateTime date) =>
        date.Month == 1 ? new DateTime(date.Year - 1, 12, 1) : new DateTime(date.Year, date.Month - 1, 1);

    private string? T(string key) => _localizationService.GetString(key);
}
