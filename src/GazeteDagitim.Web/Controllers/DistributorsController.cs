using System.ComponentModel.DataAnnotations;
using System.Reflection;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Models.ViewModels.Distributors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("distributors")]
public sealed class DistributorsController(AppDbContext dbContext) : Controller
{
    private readonly AppDbContext _dbContext = dbContext;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Distributors
            .AsNoTracking()
            .Include(distributor => distributor.DistributionDays)
            .Include(distributor => distributor.WeeklyPaymentDays)
            .Include(distributor => distributor.MonthlyPaymentDays)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(distributor => distributor.IsActive);
        }

        var distributors = await query
            .OrderBy(distributor => distributor.Name)
            .ToListAsync(cancellationToken);

        return View(new DistributorIndexViewModel
        {
            IncludeInactive = includeInactive,
            Items = distributors.Select(distributor => new DistributorListItemViewModel
            {
                Id = distributor.Id,
                Name = distributor.Name,
                Address = distributor.Address,
                Phone = distributor.Phone,
                Zone = GetDisplayName(distributor.Zone),
                PaymentType = ManagementOptions.PaymentTypeLabel(distributor.PaymentType.ToString()),
                NewspaperPrice = distributor.NewspaperPrice,
                IsActive = distributor.IsActive,
                DistributionDays = distributor.DistributionDays
                    .OrderBy(day => (int)day.Day)
                    .Select(day => ManagementOptions.DayLabel(day.Day.ToString()))
                    .ToArray(),
                PaymentDays = GetPaymentDayLabels(distributor)
            }).ToArray()
        });
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        var model = new DistributorFormViewModel();
        PopulateOptions(model);
        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        DistributorFormViewModel model,
        CancellationToken cancellationToken)
    {
        var parsed = ParseSelections(model);
        if (!ModelState.IsValid)
        {
            PopulateOptions(model);
            return View(model);
        }

        var distributor = new Distributor
        {
            Name = model.Name.Trim(),
            Address = model.Address.Trim(),
            Phone = model.Phone.Trim(),
            Zone = parsed.Zone,
            PaymentType = parsed.PaymentType,
            NewspaperPrice = model.NewspaperPrice,
            IsActive = true
        };

        ApplyNewDistributionDays(distributor, parsed.DistributionDays);
        ApplyNewPaymentDays(
            distributor,
            parsed.PaymentType,
            parsed.WeeklyPaymentDays,
            parsed.MonthlyPaymentDays);

        _dbContext.Distributors.Add(distributor);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{distributor.Name} adlı dağıtıcı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var distributor = await _dbContext.Distributors
            .AsNoTracking()
            .Include(item => item.DistributionDays)
            .Include(item => item.WeeklyPaymentDays)
            .Include(item => item.MonthlyPaymentDays)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (distributor is null)
        {
            return NotFound();
        }

        var model = new DistributorFormViewModel
        {
            Id = distributor.Id,
            Name = distributor.Name,
            Address = distributor.Address,
            Phone = distributor.Phone,
            Zone = distributor.Zone.ToString(),
            PaymentType = distributor.PaymentType.ToString(),
            NewspaperPrice = distributor.NewspaperPrice,
            DistributionDays = distributor.DistributionDays
                .OrderBy(day => (int)day.Day)
                .Select(day => day.Day.ToString())
                .ToList(),
            WeeklyPaymentDays = distributor.WeeklyPaymentDays
                .OrderBy(day => (int)day.Day)
                .Select(day => day.Day.ToString())
                .ToList(),
            MonthlyPaymentDays = distributor.MonthlyPaymentDays
                .OrderBy(day => day.DayOfMonth)
                .Select(day => day.DayOfMonth)
                .ToList()
        };

        PopulateOptions(model);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        DistributorFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        var distributor = await _dbContext.Distributors
            .Include(item => item.DistributionDays)
            .Include(item => item.WeeklyPaymentDays)
            .Include(item => item.MonthlyPaymentDays)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (distributor is null)
        {
            return NotFound();
        }

        var parsed = ParseSelections(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            PopulateOptions(model);
            return View(model);
        }

        distributor.Name = model.Name.Trim();
        distributor.Address = model.Address.Trim();
        distributor.Phone = model.Phone.Trim();
        distributor.Zone = parsed.Zone;
        distributor.PaymentType = parsed.PaymentType;
        distributor.NewspaperPrice = model.NewspaperPrice;
        SynchronizeDistributionDays(distributor, parsed.DistributionDays);
        SynchronizePaymentDays(
            distributor,
            parsed.PaymentType,
            parsed.WeeklyPaymentDays,
            parsed.MonthlyPaymentDays);

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{distributor.Name} adlı dağıtıcı güncellendi.";
        return RedirectToAction(nameof(Index), new { includeInactive = !distributor.IsActive });
    }

    [HttpPost("{id:int}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(
        int id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var distributor = await _dbContext.Distributors
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (distributor is null)
        {
            TempData["Error"] = "Dağıtıcı bulunamadı.";
            return RedirectToAction(nameof(Index), new { includeInactive });
        }

        if (distributor.IsActive)
        {
            distributor.IsActive = false;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        TempData["Notice"] =
            $"{distributor.Name} pasife alındı; teslimat ve ödeme geçmişi korundu.";
        return RedirectToAction(nameof(Index), new { includeInactive });
    }

    private ParsedDistributorSelections ParseSelections(DistributorFormViewModel model)
    {
        if (!Enum.TryParse<DistributorZone>(model.Zone, true, out var zone)
            || !Enum.IsDefined(zone))
        {
            ModelState.AddModelError(nameof(model.Zone), "Geçerli bir bölge seçin.");
        }

        if (!Enum.TryParse<PaymentType>(model.PaymentType, true, out var paymentType)
            || !Enum.IsDefined(paymentType))
        {
            ModelState.AddModelError(nameof(model.PaymentType), "Geçerli bir ödeme tipi seçin.");
        }

        var distributionDays = ParseBusinessDays(
            model.DistributionDays,
            nameof(model.DistributionDays));
        var weeklyPaymentDays = ParseBusinessDays(
            model.WeeklyPaymentDays,
            nameof(model.WeeklyPaymentDays));
        var monthlyPaymentDays = model.MonthlyPaymentDays
            .Where(day => day is >= 1 and <= 31)
            .Distinct()
            .Order()
            .ToArray();

        if (paymentType == PaymentType.Daily)
        {
            weeklyPaymentDays = [];
            monthlyPaymentDays = [];
        }
        else if (paymentType == PaymentType.Weekly)
        {
            monthlyPaymentDays = [];
        }
        else if (paymentType == PaymentType.Monthly)
        {
            weeklyPaymentDays = [];
        }

        return new ParsedDistributorSelections(
            zone,
            paymentType,
            distributionDays,
            weeklyPaymentDays,
            monthlyPaymentDays);
    }

    private List<BusinessDay> ParseBusinessDays(IEnumerable<string>? values, string propertyName)
    {
        var days = new List<BusinessDay>();

        foreach (var value in values ?? [])
        {
            if (!Enum.TryParse<BusinessDay>(value, true, out var day)
                || !Enum.IsDefined(day))
            {
                ModelState.AddModelError(propertyName, "Geçersiz bir gün seçildi.");
                continue;
            }

            if (!days.Contains(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private static void ApplyNewDistributionDays(
        Distributor distributor,
        IEnumerable<BusinessDay> days)
    {
        foreach (var day in days)
        {
            distributor.DistributionDays.Add(new DistributorDistributionDay
            {
                Distributor = distributor,
                Day = day
            });
        }
    }

    private static void ApplyNewPaymentDays(
        Distributor distributor,
        PaymentType paymentType,
        IEnumerable<BusinessDay> weeklyDays,
        IEnumerable<int> monthlyDays)
    {
        if (paymentType == PaymentType.Weekly)
        {
            foreach (var day in weeklyDays)
            {
                distributor.WeeklyPaymentDays.Add(new DistributorWeeklyPaymentDay
                {
                    Distributor = distributor,
                    Day = day
                });
            }
        }
        else if (paymentType == PaymentType.Monthly)
        {
            foreach (var day in monthlyDays)
            {
                distributor.MonthlyPaymentDays.Add(new DistributorMonthlyPaymentDay
                {
                    Distributor = distributor,
                    DayOfMonth = day
                });
            }
        }
    }

    private void SynchronizeDistributionDays(
        Distributor distributor,
        IReadOnlyCollection<BusinessDay> days)
    {
        var selected = days.ToHashSet();
        _dbContext.Set<DistributorDistributionDay>().RemoveRange(
            distributor.DistributionDays.Where(item => !selected.Contains(item.Day)));

        var existing = distributor.DistributionDays.Select(item => item.Day).ToHashSet();
        foreach (var day in selected.Where(day => !existing.Contains(day)))
        {
            distributor.DistributionDays.Add(new DistributorDistributionDay
            {
                DistributorId = distributor.Id,
                Day = day
            });
        }
    }

    private void SynchronizePaymentDays(
        Distributor distributor,
        PaymentType paymentType,
        IReadOnlyCollection<BusinessDay> weeklyDays,
        IReadOnlyCollection<int> monthlyDays)
    {
        var selectedWeekly = paymentType == PaymentType.Weekly
            ? weeklyDays.ToHashSet()
            : [];
        var selectedMonthly = paymentType == PaymentType.Monthly
            ? monthlyDays.ToHashSet()
            : [];

        _dbContext.Set<DistributorWeeklyPaymentDay>().RemoveRange(
            distributor.WeeklyPaymentDays.Where(item => !selectedWeekly.Contains(item.Day)));
        _dbContext.Set<DistributorMonthlyPaymentDay>().RemoveRange(
            distributor.MonthlyPaymentDays.Where(
                item => !selectedMonthly.Contains(item.DayOfMonth)));

        var existingWeekly = distributor.WeeklyPaymentDays
            .Select(item => item.Day)
            .ToHashSet();
        foreach (var day in selectedWeekly.Where(day => !existingWeekly.Contains(day)))
        {
            distributor.WeeklyPaymentDays.Add(new DistributorWeeklyPaymentDay
            {
                DistributorId = distributor.Id,
                Day = day
            });
        }

        var existingMonthly = distributor.MonthlyPaymentDays
            .Select(item => item.DayOfMonth)
            .ToHashSet();
        foreach (var day in selectedMonthly.Where(day => !existingMonthly.Contains(day)))
        {
            distributor.MonthlyPaymentDays.Add(new DistributorMonthlyPaymentDay
            {
                DistributorId = distributor.Id,
                DayOfMonth = day
            });
        }
    }

    private static IReadOnlyList<string> GetPaymentDayLabels(Distributor distributor)
    {
        if (distributor.PaymentType == PaymentType.Weekly)
        {
            return distributor.WeeklyPaymentDays
                .OrderBy(day => (int)day.Day)
                .Select(day => ManagementOptions.DayLabel(day.Day.ToString()))
                .ToArray();
        }

        if (distributor.PaymentType == PaymentType.Monthly)
        {
            return distributor.MonthlyPaymentDays
                .OrderBy(day => day.DayOfMonth)
                .Select(day => $"{day.DayOfMonth}. gün")
                .ToArray();
        }

        return ["Her gün"];
    }

    private static void PopulateOptions(DistributorFormViewModel model)
    {
        model.ZoneOptions = Enum.GetValues<DistributorZone>()
            .Select(zone => new SelectListItem
            {
                Value = zone.ToString(),
                Text = GetDisplayName(zone),
                Selected = string.Equals(
                    model.Zone,
                    zone.ToString(),
                    StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
    }

    private static string GetDisplayName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName()
            ?? value.ToString();
    }

    private sealed record ParsedDistributorSelections(
        DistributorZone Zone,
        PaymentType PaymentType,
        IReadOnlyCollection<BusinessDay> DistributionDays,
        IReadOnlyCollection<BusinessDay> WeeklyPaymentDays,
        IReadOnlyCollection<int> MonthlyPaymentDays);
}
