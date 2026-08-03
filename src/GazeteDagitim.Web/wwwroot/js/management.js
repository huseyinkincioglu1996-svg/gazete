(() => {
  "use strict";

  const updateNewspaperDayConflicts = (root) => {
    const inputs = Array.from(root.querySelectorAll("[data-newspaper-day]"));
    if (inputs.length === 0) return;

    const byDay = new Map(inputs.map((input) => [input.dataset.newspaperDay, input]));
    const combined = byDay.get("SundayMonday");
    const sunday = byDay.get("Sunday");
    const monday = byDay.get("Monday");
    if (!combined || !sunday || !monday) return;

    if (combined.checked) {
      sunday.checked = false;
      monday.checked = false;
    } else if (sunday.checked || monday.checked) {
      combined.checked = false;
    }

    combined.disabled = sunday.checked || monday.checked;
    sunday.disabled = combined.checked;
    monday.disabled = combined.checked;
  };

  document.querySelectorAll("[data-newspaper-days]").forEach((root) => {
    updateNewspaperDayConflicts(root);
    root.addEventListener("change", () => updateNewspaperDayConflicts(root));
  });

  const updatePaymentSections = (root) => {
    const select = root.querySelector("[data-payment-type]");
    if (!select) return;

    root.querySelectorAll("[data-payment-section]").forEach((section) => {
      const visible = section.dataset.paymentSection === select.value;
      section.hidden = !visible;
      section.querySelectorAll("input").forEach((input) => {
        input.disabled = !visible;
      });
    });
  };

  document.querySelectorAll("[data-payment-plan]").forEach((root) => {
    updatePaymentSections(root);
    root.querySelector("[data-payment-type]")
      ?.addEventListener("change", () => updatePaymentSections(root));
  });

  const invariantNumber = (value) => {
    const normalized = String(value ?? "").trim().replace(",", ".");
    const number = Number.parseFloat(normalized);
    return Number.isFinite(number) ? number : null;
  };

  const updateMapLink = (root) => {
    const latitude = invariantNumber(root.querySelector("[data-latitude]")?.value);
    const longitude = invariantNumber(root.querySelector("[data-longitude]")?.value);
    const link = root.querySelector("[data-map-link]");
    if (!link) return;

    const valid = latitude !== null
      && longitude !== null
      && latitude >= -90
      && latitude <= 90
      && longitude >= -180
      && longitude <= 180;

    link.hidden = !valid;
    if (valid) {
      link.href = `https://www.google.com/maps?q=${latitude},${longitude}`;
    } else {
      link.removeAttribute("href");
    }
  };

  document.querySelectorAll("[data-location-picker]").forEach((root) => {
    const latitudeInput = root.querySelector("[data-latitude]");
    const longitudeInput = root.querySelector("[data-longitude]");
    const locateButton = root.querySelector("[data-use-location]");
    const message = root.querySelector("[data-location-message]");

    updateMapLink(root);
    latitudeInput?.addEventListener("input", () => updateMapLink(root));
    longitudeInput?.addEventListener("input", () => updateMapLink(root));

    locateButton?.addEventListener("click", () => {
      if (!navigator.geolocation) {
        if (message) message.textContent = "Bu cihaz konum özelliğini desteklemiyor.";
        return;
      }

      locateButton.disabled = true;
      if (message) message.textContent = "Konum alınıyor…";

      navigator.geolocation.getCurrentPosition(
        (position) => {
          if (latitudeInput) latitudeInput.value = position.coords.latitude.toFixed(7);
          if (longitudeInput) longitudeInput.value = position.coords.longitude.toFixed(7);
          if (message) message.textContent = "Konum alanları güncellendi.";
          locateButton.disabled = false;
          updateMapLink(root);
        },
        (error) => {
          const descriptions = {
            1: "Konum izni verilmedi.",
            2: "Konum bilgisi alınamadı.",
            3: "Konum isteği zaman aşımına uğradı."
          };
          if (message) {
            message.textContent = descriptions[error.code] ?? "Konum alınamadı.";
          }
          locateButton.disabled = false;
        },
        {
          enableHighAccuracy: true,
          timeout: 12000,
          maximumAge: 30000
        }
      );
    });
  });

  document.querySelectorAll("form[data-confirm]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
      }
    });
  });

  document.querySelectorAll("[data-row-link]").forEach((row) => {
    const targetUrl = row.dataset.rowLink;
    if (!targetUrl) return;

    const isInteractive = (target) =>
      target instanceof Element
      && Boolean(target.closest("a, button, input, select, textarea, label, form"));

    row.addEventListener("click", (event) => {
      if (!isInteractive(event.target)) {
        window.location.assign(targetUrl);
      }
    });

    row.addEventListener("keydown", (event) => {
      if (event.target !== row || (event.key !== "Enter" && event.key !== " ")) {
        return;
      }

      event.preventDefault();
      window.location.assign(targetUrl);
    });
  });
})();
