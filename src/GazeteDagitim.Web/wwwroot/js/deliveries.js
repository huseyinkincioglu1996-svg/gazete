(() => {
  "use strict";

  const currency = new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  });

  const parseAmount = (value) => {
    const parsed = Number.parseFloat(String(value || "0").replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const cashSaleDialog = document.querySelector("[data-cash-sale-dialog]");
  if (cashSaleDialog) {
    const cashSaleOpen = document.querySelector("[data-cash-sale-open]");
    const cashSaleForm = cashSaleDialog.querySelector("[data-cash-sale-form]");
    const distributorField = cashSaleDialog.querySelector(
      "[data-cash-sale-distributor]",
    );
    const quantityField = cashSaleDialog.querySelector(
      "[data-cash-sale-quantity]",
    );
    const unitPriceOutput = cashSaleDialog.querySelector(
      "[data-cash-sale-unit-price]",
    );
    const quantityOutput = cashSaleDialog.querySelector(
      "[data-cash-sale-quantity-output]",
    );
    const totalOutput = cashSaleDialog.querySelector("[data-cash-sale-total]");

    const closeCashSaleDialog = () => {
      if (cashSaleDialog.open) cashSaleDialog.close();
    };

    const updateCashSaleCalculation = () => {
      const selectedOption = distributorField?.selectedOptions?.[0];
      const unitPrice = parseAmount(selectedOption?.dataset.unitPrice);
      const parsedQuantity = Number.parseInt(quantityField?.value || "1", 10);
      const quantity = Math.min(
        1000,
        Math.max(1, Number.isFinite(parsedQuantity) ? parsedQuantity : 1),
      );

      if (unitPriceOutput) {
        unitPriceOutput.textContent = currency.format(unitPrice);
      }
      if (quantityOutput) quantityOutput.textContent = String(quantity);
      if (totalOutput) {
        totalOutput.textContent = currency.format(unitPrice * quantity);
      }
    };

    cashSaleOpen?.addEventListener("click", () => {
      updateCashSaleCalculation();
      cashSaleDialog.showModal();
    });
    cashSaleDialog
      .querySelectorAll("[data-cash-sale-close]")
      .forEach((button) =>
        button.addEventListener("click", closeCashSaleDialog),
      );
    cashSaleDialog.addEventListener("click", (event) => {
      if (event.target === cashSaleDialog) closeCashSaleDialog();
    });
    distributorField?.addEventListener("change", updateCashSaleCalculation);
    quantityField?.addEventListener("input", updateCashSaleCalculation);
    cashSaleForm?.addEventListener("submit", () => {
      const submitButton = cashSaleForm.querySelector("[data-cash-sale-submit]");
      if (!cashSaleForm.checkValidity() || !submitButton) return;

      submitButton.disabled = true;
      submitButton.textContent = "Tahsilata ekleniyor…";
    });
  }

  const form = document.querySelector("#daily-deliveries-form");
  if (!form) return;

  const rowsContainer = form.querySelector("[data-delivery-row]")?.parentElement;
  const dateField = form.querySelector("[data-delivery-date]");
  const antiforgeryField = form.querySelector(
    'input[name="__RequestVerificationToken"]',
  );
  const autosaveUrl = form.dataset.autosaveUrl;
  const cashLocked = form.dataset.cashLocked === "true";
  const cashSaleTotal = parseAmount(form.dataset.cashSaleTotal);
  const cashSaleCount = Number.parseInt(form.dataset.cashSaleCount || "0", 10);
  const listMode = form.dataset.listMode || "deliveries";
  const deliveredOutput = document.querySelector("#delivered-total");
  const collectedOutput = document.querySelector("#collected-total");
  const collectedCountOutput = document.querySelector("#collected-count");
  const pendingPaymentOutput = document.querySelector("#pending-payment-count");
  const collectionRowCountOutput = document.querySelector(
    "#collection-row-count",
  );
  const pageStatus = document.querySelector("#autosave-page-status");
  const persistedStates = new WeakMap();
  const amountTimers = new WeakMap();
  let activeSaveCount = 0;
  let pendingAmountCount = 0;
  let processedOrderSequence = 0;

  if (!rowsContainer || !dateField || !antiforgeryField || !autosaveUrl) {
    return;
  }

  const getRowState = (row) => ({
    delivered: row.dataset.delivered === "true",
    collected: row.dataset.collected === "true",
    amount: parseAmount(row.querySelector("[data-amount]")?.value),
    paymentMethod:
      row.querySelector("[data-payment-field]:not([data-amount])")?.value ||
      "Nakit",
  });

  const setRowStatus = (row, message, kind = "") => {
    const status = row.querySelector("[data-row-status]");
    if (!status) return;

    status.textContent = message;
    status.classList.toggle("is-success", kind === "success");
    status.classList.toggle("is-error", kind === "error");
  };

  const setPageStatus = (message, kind = "") => {
    if (!pageStatus) return;

    pageStatus.textContent = message;
    pageStatus.classList.toggle("is-success", kind === "success");
    pageStatus.classList.toggle("is-error", kind === "error");
  };

  const syncControlAvailability = (row) => {
    const saving = row.classList.contains("is-saving");
    const collected = row.dataset.collected === "true";
    const paymentAvailable =
      row.dataset.paymentDue === "true" || collected;

    row.querySelectorAll("[data-payment-cell]").forEach((cell) => {
      cell.classList.toggle("payment-not-due", !paymentAvailable);
    });
    const deliveryToggle = row.querySelector("[data-delivered-toggle]");
    if (deliveryToggle) {
      deliveryToggle.disabled = cashLocked || saving;
    }
    const collectionToggle = row.querySelector("[data-collected-toggle]");
    if (collectionToggle) {
      collectionToggle.disabled = cashLocked || saving || !paymentAvailable;
    }
    row.querySelectorAll("[data-payment-field]").forEach((field) => {
      field.disabled = cashLocked || saving || !collected || !paymentAvailable;
    });
  };

  const applyRowState = (row, state) => {
    const delivered = Boolean(state.delivered);
    const collected = Boolean(state.collected);
    const amount = parseAmount(state.amount);
    const method = state.paymentMethod || "Nakit";
    const deliveredButton = row.querySelector("[data-delivered-toggle]");
    const collectedButton = row.querySelector("[data-collected-toggle]");
    const amountField = row.querySelector("[data-amount]");
    const methodField = row.querySelector(
      "[data-payment-field]:not([data-amount])",
    );

    row.dataset.delivered = String(delivered);
    row.dataset.collected = String(collected);
    row.classList.toggle(
      "is-processed",
      listMode === "collections" ? collected : delivered || collected,
    );

    if (deliveredButton) {
      deliveredButton.setAttribute("aria-pressed", String(delivered));
      deliveredButton.classList.toggle("is-active", delivered);
      const icon = deliveredButton.querySelector(".tracking-toggle-icon");
      const label = deliveredButton.querySelector("[data-toggle-label]");
      if (icon) {
        icon.textContent = delivered
          ? "✓"
          : deliveredButton.hasAttribute("data-icon-only")
            ? ""
            : "○";
      }
      if (label) label.textContent = delivered ? "Teslim edildi" : "Teslim et";
    }

    if (collectedButton) {
      collectedButton.setAttribute("aria-pressed", String(collected));
      collectedButton.classList.toggle("is-active", collected);
      const icon = collectedButton.querySelector(".tracking-toggle-icon");
      const label = collectedButton.querySelector("[data-toggle-label]");
      if (icon) {
        icon.textContent = collected
          ? "✓"
          : collectedButton.hasAttribute("data-icon-only")
            ? ""
            : "○";
      }
      if (label) label.textContent = collected ? "Ödeme alındı" : "Ödeme al";
    }

    if (amountField) amountField.value = amount.toFixed(2);
    if (methodField) methodField.value = method;
    syncControlAvailability(row);
  };

  const updateTotals = (summary = null) => {
    const rows = [...rowsContainer.querySelectorAll("[data-delivery-row]")];
    const deliveredCount = summary?.deliveredCount ??
      rows.filter((row) => row.dataset.delivered === "true").length;
    const collectedRows = rows.filter(
      (row) => row.dataset.collected === "true",
    );
    const collectedCount = summary?.collectedCount ?? collectedRows.length;
    const subscriberCollectedTotal = summary?.collectedTotal ??
      collectedRows.reduce(
        (sum, row) => sum + parseAmount(row.querySelector("[data-amount]")?.value),
        0,
      );
    const collectedTotal = subscriberCollectedTotal + cashSaleTotal;

    if (deliveredOutput) deliveredOutput.textContent = String(deliveredCount);
    if (collectedOutput) {
      collectedOutput.textContent = currency.format(collectedTotal);
    }
    if (collectedCountOutput) {
      collectedCountOutput.textContent =
        `${collectedCount} ödeme · ${cashSaleCount} nakit satış`;
    }
    if (pendingPaymentOutput) {
      pendingPaymentOutput.textContent = String(
        rows.filter((row) => row.dataset.collected !== "true").length,
      );
    }
    if (collectionRowCountOutput) {
      collectionRowCountOutput.textContent = String(rows.length);
    }
  };

  const sortRows = () => {
    const rows = [...rowsContainer.querySelectorAll("[data-delivery-row]")];
    rows.sort((left, right) => {
      const leftProcessed = listMode === "collections"
        ? left.dataset.collected === "true"
        : left.dataset.delivered === "true" ||
          left.dataset.collected === "true";
      const rightProcessed = listMode === "collections"
        ? right.dataset.collected === "true"
        : right.dataset.delivered === "true" ||
          right.dataset.collected === "true";

      if (leftProcessed !== rightProcessed) {
        return Number(leftProcessed) - Number(rightProcessed);
      }

      if (leftProcessed) {
        return Number(left.dataset.processedOrder) -
          Number(right.dataset.processedOrder);
      }

      return Number(left.dataset.originalOrder) -
        Number(right.dataset.originalOrder);
    });

    rows.forEach((row) => rowsContainer.append(row));
  };

  const clearAmountTimer = (row) => {
    const timer = amountTimers.get(row);
    if (timer) {
      window.clearTimeout(timer);
      pendingAmountCount = Math.max(0, pendingAmountCount - 1);
    }
    amountTimers.delete(row);
  };

  const normalizeServerRow = (row, responseRow) => ({
    delivered: responseRow?.delivered ?? row.dataset.delivered === "true",
    collected: responseRow?.collected ?? row.dataset.collected === "true",
    amount: responseRow?.amount ??
      parseAmount(row.querySelector("[data-amount]")?.value),
    paymentMethod: responseRow?.paymentMethod ||
      row.querySelector("[data-payment-field]:not([data-amount])")?.value ||
      "Nakit",
  });

  const saveRow = async (
    row,
    patch,
    successMessage,
    moveProcessedToEnd = false,
  ) => {
    const previousState = persistedStates.get(row) || getRowState(row);
    const body = new URLSearchParams({
      __RequestVerificationToken: antiforgeryField.value,
      Date: dateField.value,
      SubscriberId: row.dataset.subscriberId,
    });

    Object.entries(patch).forEach(([key, value]) => {
      body.set(key, String(value));
    });

    activeSaveCount += 1;
    row.classList.add("is-saving");
    row.classList.remove("is-save-error");
    syncControlAvailability(row);
    setRowStatus(row, "Kaydediliyor…");
    setPageStatus("Değişiklik kaydediliyor…");

    try {
      const response = await window.fetch(autosaveUrl, {
        method: "POST",
        headers: {
          "Accept": "application/json",
          "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
          "X-Requested-With": "XMLHttpRequest",
        },
        body: body.toString(),
      });
      const payload = await response.json().catch(() => null);

      if (!response.ok || !payload?.success) {
        throw new Error(
          payload?.message || "Değişiklik kaydedilemedi. Yeniden deneyin.",
        );
      }

      const persistedState = normalizeServerRow(row, payload.row);
      persistedStates.set(row, persistedState);
      applyRowState(row, persistedState);
      if (
        listMode === "collections" &&
        !persistedState.collected &&
        row.dataset.paymentDue !== "true"
      ) {
        row.remove();
      }
      if (!persistedState.delivered && !persistedState.collected) {
        delete row.dataset.processedOrder;
      } else if (moveProcessedToEnd) {
        processedOrderSequence += 1;
        row.dataset.processedOrder = String(processedOrderSequence);
      }
      updateTotals(payload.summary);
      sortRows();
      setRowStatus(
        row,
        payload.message || successMessage || "Kaydedildi",
        "success",
      );
      setPageStatus("Son işlem otomatik kaydedildi.", "success");
    } catch (error) {
      applyRowState(row, previousState);
      updateTotals();
      sortRows();
      row.classList.add("is-save-error");
      setRowStatus(row, error.message, "error");
      setPageStatus(error.message, "error");
    } finally {
      activeSaveCount = Math.max(0, activeSaveCount - 1);
      row.classList.remove("is-saving");
      syncControlAvailability(row);
      if (activeSaveCount === 0 && !pageStatus?.classList.contains("is-error")) {
        setPageStatus("Tüm değişiklikler kaydedildi.", "success");
      }
    }
  };

  const rows = [...rowsContainer.querySelectorAll("[data-delivery-row]")];
  rows.forEach((row) => {
    persistedStates.set(row, getRowState(row));
    syncControlAvailability(row);

    row.querySelector("[data-delivered-toggle]")?.addEventListener(
      "click",
      () => {
        clearAmountTimer(row);
        const state = getRowState(row);
        const nextState = { ...state, delivered: !state.delivered };
        applyRowState(row, nextState);
        updateTotals();
        const patch = { Delivered: nextState.delivered };
        if (nextState.collected) {
          patch.Amount = nextState.amount;
          patch.PaymentMethod = nextState.paymentMethod;
        }
        saveRow(
          row,
          patch,
          nextState.delivered ? "Teslimat kaydedildi." : "Teslimat geri alındı.",
          true,
        );
      },
    );

    row.querySelector("[data-collected-toggle]")?.addEventListener(
      "click",
      () => {
        clearAmountTimer(row);
        const state = getRowState(row);
        const nextCollected = !state.collected;
        const amountField = row.querySelector("[data-amount]");

        if (nextCollected && state.amount <= 0) {
          setRowStatus(row, "Önce sıfırdan büyük bir ödeme tutarı girin.", "error");
          setPageStatus(
            "Ödeme alınabilmesi için tutar sıfırdan büyük olmalıdır.",
            "error",
          );
          if (amountField) {
            amountField.disabled = false;
            amountField.focus();
          }
          return;
        }

        const nextState = { ...state, collected: nextCollected };
        applyRowState(row, nextState);
        updateTotals();
        const patch = { Collected: nextCollected };
        if (nextCollected) {
          patch.Amount = nextState.amount;
          patch.PaymentMethod = nextState.paymentMethod;
        }
        saveRow(
          row,
          patch,
          nextCollected ? "Ödeme kaydedildi." : "Ödeme geri alındı.",
          true,
        );
      },
    );

    const amountField = row.querySelector("[data-amount]");
    amountField?.addEventListener("input", () => {
      if (row.dataset.collected !== "true") return;

      clearAmountTimer(row);
      const amount = parseAmount(amountField.value);
      updateTotals();
      if (amount <= 0) {
        setRowStatus(row, "Tutar sıfırdan büyük olmalıdır.", "error");
        return;
      }

      setRowStatus(row, "Tutar kaydedilecek…");
      pendingAmountCount += 1;
      amountTimers.set(
        row,
        window.setTimeout(() => {
          amountTimers.delete(row);
          pendingAmountCount = Math.max(0, pendingAmountCount - 1);
          const state = getRowState(row);
          saveRow(
            row,
            {
              Amount: state.amount,
              PaymentMethod: state.paymentMethod,
            },
            "Ödeme tutarı güncellendi.",
          );
        }, 500),
      );
    });

    amountField?.addEventListener("change", () => {
      if (row.dataset.collected !== "true") return;

      clearAmountTimer(row);
      const state = getRowState(row);
      if (state.amount <= 0) {
        setRowStatus(row, "Tutar sıfırdan büyük olmalıdır.", "error");
        return;
      }
      saveRow(
        row,
        {
          Amount: state.amount,
          PaymentMethod: state.paymentMethod,
        },
        "Ödeme tutarı güncellendi.",
      );
    });

    row.querySelector("[data-payment-field]:not([data-amount])")
      ?.addEventListener("change", () => {
        if (row.dataset.collected !== "true") return;

        clearAmountTimer(row);
        const state = getRowState(row);
        saveRow(
          row,
          {
            Amount: state.amount,
            PaymentMethod: state.paymentMethod,
          },
          "Ödeme yöntemi güncellendi.",
        );
      });
  });

  form.addEventListener("submit", (event) => event.preventDefault());
  document.querySelectorAll("[data-dirty-date-form]").forEach((dateForm) => {
    dateForm.addEventListener("submit", (event) => {
      if (activeSaveCount === 0 && pendingAmountCount === 0) return;

      event.preventDefault();
      setPageStatus(
        "Önce devam eden otomatik kaydın tamamlanmasını bekleyin.",
        "error",
      );
    });
  });
  window.addEventListener("beforeunload", (event) => {
    if (activeSaveCount === 0 && pendingAmountCount === 0) return;

    event.preventDefault();
    event.returnValue = "";
  });
  rows.forEach((row) => {
    const isProcessed = listMode === "collections"
      ? row.dataset.collected === "true"
      : row.dataset.delivered === "true" ||
        row.dataset.collected === "true";
    if (isProcessed) {
      processedOrderSequence += 1;
      row.dataset.processedOrder = String(processedOrderSequence);
    }
  });
  sortRows();
  updateTotals();
})();
