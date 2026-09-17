(() => {
  "use strict";

  const scheduleType = document.querySelector("#ScheduleType");
  const dayCount = document.querySelector("#DayCount");
  const collectionDay = document.querySelector("#CollectionDayOfMonth");
  const hint = document.querySelector("[data-collection-day-hint]");
  const amountHint = document.querySelector("[data-collection-amount-hint]");
  const dailyNote = document.querySelector("[data-daily-schedule-note]");
  const weeklyNote = document.querySelector("[data-weekly-schedule-note]");
  const monthlyFields = document.querySelectorAll(
    "[data-monthly-schedule-field]",
  );
  if (
    !scheduleType ||
    !dayCount ||
    !collectionDay ||
    !hint ||
    !amountHint ||
    !dailyNote ||
    !weeklyNote
  ) return;

  const defaultHint = hint.dataset.defaultText || hint.textContent.trim();
  const defaultAmountHint =
    amountHint.dataset.defaultText || amountHint.textContent.trim();
  const startsMonthly = scheduleType.value === "monthly";
  let monthlyDayCount = startsMonthly ? dayCount.value || "30" : "30";
  let monthlyCollectionDay = startsMonthly
    ? collectionDay.value || "1"
    : "1";

  const syncTenDaySchedule = () => {
    const isTenDaySchedule = Number.parseInt(dayCount.value, 10) === 10;
    collectionDay.readOnly = isTenDaySchedule;
    if (isTenDaySchedule) {
      collectionDay.value = "10";
      hint.textContent =
        "10 günlük planda ödeme günleri otomatik olarak ayın 10., 20. ve son günüdür.";
      return;
    }

    hint.textContent = defaultHint;
  };

  const syncScheduleType = () => {
    const isDaily = scheduleType.value === "daily";
    const isWeekly = scheduleType.value === "weekly";
    const isFixedInterval = isDaily || isWeekly;
    monthlyFields.forEach((field) => {
      field.hidden = isFixedInterval;
    });
    dailyNote.hidden = !isDaily;
    weeklyNote.hidden = !isWeekly;

    if (isFixedInterval) {
      dayCount.value = isWeekly ? "7" : "1";
      collectionDay.value = "1";
      dayCount.readOnly = true;
      collectionDay.readOnly = true;
      amountHint.textContent =
        isWeekly
          ? "Bu tutar her 7 günlük dönem için tahsil edilecek tutardır."
          : "Bu tutar her takvim günü için ayrı tahsilat tutarıdır.";
      return;
    }

    dayCount.readOnly = false;
    if (dayCount.value === "1") {
      dayCount.value = monthlyDayCount || "30";
    }
    if (!collectionDay.value) {
      collectionDay.value = monthlyCollectionDay || "1";
    }
    amountHint.textContent = defaultAmountHint;
    syncTenDaySchedule();
  };

  scheduleType.addEventListener("change", syncScheduleType);
  dayCount.addEventListener("input", () => {
    if (scheduleType.value === "monthly") {
      monthlyDayCount = dayCount.value;
      syncTenDaySchedule();
    }
  });
  dayCount.addEventListener("change", () => {
    if (scheduleType.value === "monthly") {
      syncTenDaySchedule();
    }
  });
  collectionDay.addEventListener("input", () => {
    if (scheduleType.value === "monthly" && collectionDay.value) {
      monthlyCollectionDay = collectionDay.value;
    }
  });
  syncScheduleType();
})();
