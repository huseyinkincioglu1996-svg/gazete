(() => {
  const form = document.querySelector("#cash-form");
  const body = document.querySelector("#cash-items");
  const template = document.querySelector("#cash-row-template");
  const addButton = document.querySelector("#add-cash-row");
  const statusInput = document.querySelector("#cash-status-input");
  const totalOutput = document.querySelector("#cash-daily-total");
  if (!form || !body || !template || !statusInput || !totalOutput) return;

  const currency = new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  });

  const reindex = () => {
    [...body.querySelectorAll("[data-cash-row]")].forEach((row, index) => {
      row.querySelectorAll("[data-name]").forEach((field) => {
        field.name = `Items[${index}].${field.dataset.name}`;
      });
      const namedFields = row.querySelectorAll(
        'input[name*=".SubscriberName"], input[name*=".Amount"], input[name*=".Description"]',
      );
      namedFields.forEach((field) => {
        const suffix = field.name.split(".").pop();
        field.name = `Items[${index}].${suffix}`;
      });
    });
  };

  const updateTotal = () => {
    const automatic = [...body.querySelectorAll("[data-auto-amount]")].reduce(
      (sum, cell) => sum + (Number.parseFloat(cell.dataset.autoAmount || "0") || 0),
      0,
    );
    const manual = [...body.querySelectorAll("[data-cash-amount]")].reduce(
      (sum, input) => sum + (Number.parseFloat(input.value || "0") || 0),
      0,
    );
    totalOutput.textContent = currency.format(automatic + manual);
  };

  body.addEventListener("click", (event) => {
    const button = event.target.closest("[data-remove-cash-row]");
    if (!button) return;
    button.closest("[data-cash-row]")?.remove();
    reindex();
    updateTotal();
  });
  body.addEventListener("input", updateTotal);
  addButton?.addEventListener("click", () => {
    body.append(template.content.cloneNode(true));
    reindex();
    updateTotal();
    body.querySelector("[data-cash-row]:last-of-type input")?.focus();
  });
  form.querySelectorAll("[data-cash-submit]").forEach((button) => {
    button.addEventListener("click", (event) => {
      const confirmation = button.dataset.confirm;
      if (confirmation && !window.confirm(confirmation)) {
        event.preventDefault();
        return;
      }
      statusInput.value = button.dataset.cashSubmit || "Taslak";
    });
  });
  form.addEventListener("submit", reindex);

  reindex();
  updateTotal();
})();
