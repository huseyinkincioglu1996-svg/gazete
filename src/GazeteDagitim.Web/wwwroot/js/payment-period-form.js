(() => {
  "use strict";

  const scheduleType = document.querySelector("#ScheduleType");
  const dayCount = document.querySelector("#DayCount");
  const collectionDay = document.querySelector("#CollectionDayOfMonth");
  const hint = document.querySelector("[data-collection-day-hint]");
  const amountHint = document.querySelector("[data-collection-amount-hint]");
  const dailyNote = document.querySelector("[data-daily-schedule-note]");
  const weeklyNote = document.querySelector("[data-weekly-schedule-note]");
  const tenDayNote = document.querySelector("[data-ten-day-schedule-note]");
  const form = scheduleType?.closest("form");
  const submitButton = form?.querySelector('button[type="submit"]');
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
    !weeklyNote ||
    !tenDayNote
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
    const isTenDay = scheduleType.value === "ten-day";
    const isFixedInterval = isDaily || isWeekly || isTenDay;
    monthlyFields.forEach((field) => {
      field.hidden = isFixedInterval;
    });
    dailyNote.hidden = !isDaily;
    weeklyNote.hidden = !isWeekly;
    tenDayNote.hidden = !isTenDay;

    if (isFixedInterval) {
      dayCount.value = isTenDay ? "10" : isWeekly ? "7" : "1";
      collectionDay.value = isTenDay ? "10" : "1";
      dayCount.readOnly = true;
      collectionDay.readOnly = true;
      amountHint.textContent =
        isTenDay
          ? "Bu tutar 10 günlük temel tutardır; ayın son dilimi kalan gün sayısına göre oranlanır."
          : isWeekly
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

  scheduleType.addEventListener("change", () => {
    if (scheduleType.value === "monthly") {
      dayCount.value = monthlyDayCount || "30";
      collectionDay.value = monthlyCollectionDay || "1";
    }
    syncScheduleType();
  });
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

  if (form && submitButton) {
    const originalButtonText = submitButton.textContent.trim();

    form.addEventListener("submit", (event) => {
      if (form.dataset.submitPending === "true") {
        event.preventDefault();
        event.stopImmediatePropagation();
        return;
      }

      const validator = window.jQuery?.(form);
      if (typeof validator?.valid === "function" && !validator.valid()) {
        return;
      }

      form.dataset.submitPending = "true";
      submitButton.disabled = true;
      submitButton.setAttribute("aria-disabled", "true");
      submitButton.textContent = "Kaydediliyor...";
    });

    window.addEventListener("pageshow", () => {
      delete form.dataset.submitPending;
      if (submitButton.hasAttribute("data-offline-lock-managed")) {
        submitButton.setAttribute("data-offline-original-disabled", "false");
      } else {
        submitButton.disabled = false;
        submitButton.removeAttribute("aria-disabled");
      }
      submitButton.textContent = originalButtonText;
    });
  }

  syncScheduleType();
})();
