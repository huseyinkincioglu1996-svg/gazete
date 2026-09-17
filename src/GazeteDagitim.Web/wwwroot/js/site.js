(() => {
  "use strict";

  const banner = document.querySelector("[data-offline-banner]");
  const mutatingFormSelector = 'form[method="post" i], form[data-autosave-url]';
  const standaloneActionSelector = "[data-cash-sale-open]";
  const controlSelector = [
    "button:not([data-cash-sale-close])",
    "input:not([type=hidden])",
    "select",
    "textarea",
  ].join(", ");
  const managedAttribute = "data-offline-lock-managed";
  const originalDisabledAttribute = "data-offline-original-disabled";

  if (!banner) {
    return;
  }

  const rememberAndDisable = (control) => {
    if (!control.hasAttribute(managedAttribute)) {
      control.setAttribute(
        originalDisabledAttribute,
        control.disabled ? "true" : "false"
      );
      control.setAttribute(managedAttribute, "true");
    }

    control.disabled = true;
    control.setAttribute("aria-disabled", "true");
  };

  const restoreControl = (control) => {
    if (!control.hasAttribute(managedAttribute)) {
      return;
    }

    control.disabled = control.getAttribute(originalDisabledAttribute) === "true";
    control.removeAttribute(managedAttribute);
    control.removeAttribute(originalDisabledAttribute);
    control.removeAttribute("aria-disabled");
  };

  const affectedControls = () => {
    const controls = new Set();

    document.querySelectorAll(mutatingFormSelector).forEach((form) => {
      form.querySelectorAll(controlSelector).forEach((control) => {
        controls.add(control);
      });
    });
    document.querySelectorAll(standaloneActionSelector).forEach((control) => {
      controls.add(control);
    });

    return controls;
  };

  const announceOffline = () => {
    banner.hidden = false;
    banner.classList.remove("network-status-banner--pulse");
    window.requestAnimationFrame(() => {
      banner.classList.add("network-status-banner--pulse");
    });
  };

  const syncNetworkState = () => {
    const offline = !navigator.onLine;
    document.body.classList.toggle("is-offline", offline);
    banner.hidden = !offline;

    affectedControls().forEach((control) => {
      if (offline) {
        rememberAndDisable(control);
      } else {
        restoreControl(control);
      }
    });
  };

  const isMutatingForm = (target) => target instanceof HTMLFormElement
    && target.matches(mutatingFormSelector);

  const isMutatingControl = (target) => {
    if (!(target instanceof Element)) {
      return false;
    }

    const control = target.closest(
      `${controlSelector}, ${standaloneActionSelector}`
    );
    return Boolean(control && (
      control.matches(standaloneActionSelector)
      || control.closest(mutatingFormSelector)
    ));
  };

  document.addEventListener("submit", (event) => {
    if (navigator.onLine || !isMutatingForm(event.target)) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    announceOffline();
  }, true);

  document.addEventListener("click", (event) => {
    if (navigator.onLine || !isMutatingControl(event.target)) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    announceOffline();
  }, true);

  window.addEventListener("online", syncNetworkState);
  window.addEventListener("offline", syncNetworkState);
  window.addEventListener("pageshow", syncNetworkState);
  document.addEventListener("visibilitychange", () => {
    if (!document.hidden) {
      syncNetworkState();
    }
  });
  syncNetworkState();
})();

(() => {
  "use strict";

  const panel = document.querySelector("[data-pwa-panel]");
  const primaryButton = document.querySelector("[data-pwa-primary]");
  const dismissButton = document.querySelector("[data-pwa-dismiss]");
  const title = document.querySelector("[data-pwa-title]");
  const copy = document.querySelector("[data-pwa-copy]");

  if (!("serviceWorker" in navigator) || !panel || !primaryButton
      || !dismissButton || !title || !copy) {
    return;
  }

  const standalone = window.matchMedia("(display-mode: standalone)").matches
    || window.navigator.standalone === true;
  const ios = /iphone|ipad|ipod/i.test(navigator.userAgent)
    || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  let deferredInstallPrompt = null;
  let waitingWorker = null;
  let panelMode = "";
  let refreshing = false;

  if (standalone) {
    document.body.classList.add("pwa-standalone");
  }

  const wasDismissed = (mode) => {
    try {
      return window.sessionStorage.getItem(`gazete-pwa-dismissed-${mode}`) === "1";
    } catch {
      return false;
    }
  };

  const rememberDismissal = (mode) => {
    try {
      window.sessionStorage.setItem(`gazete-pwa-dismissed-${mode}`, "1");
    } catch {
      // Bazı gizli tarama modlarında sessionStorage kullanılamayabilir.
    }
  };

  const hidePanel = () => {
    panel.hidden = true;
    panelMode = "";
  };

  const showPanel = (mode) => {
    if (standalone || wasDismissed(mode)) {
      return;
    }

    panelMode = mode;
    panel.classList.toggle("pwa-action-panel--update", mode === "update");
    panel.classList.toggle("pwa-action-panel--ios", mode === "ios");
    primaryButton.hidden = mode === "ios";

    if (mode === "update") {
      title.textContent = "Yeni sürüm hazır";
      copy.textContent = "Güncel sürümü açmak için sayfayı güvenle yenileyin.";
      primaryButton.textContent = "Şimdi yenile";
    } else if (mode === "ios") {
      title.textContent = "Gazete Dağıtım uygulamasını yükle";
      copy.textContent = "Paylaş simgesine dokunun, ardından Ana Ekrana Ekle seçeneğini seçin.";
    } else {
      title.textContent = "Gazete Dağıtım uygulamasını yükle";
      copy.textContent = "Uygulamayı ana ekrandan hızlıca açabilirsiniz.";
      primaryButton.textContent = "Uygulamayı yükle";
    }

    panel.hidden = false;
  };

  dismissButton.addEventListener("click", () => {
    if (panelMode) {
      rememberDismissal(panelMode);
    }
    hidePanel();
  });

  primaryButton.addEventListener("click", async () => {
    if (panelMode === "update" && waitingWorker) {
      primaryButton.disabled = true;
      primaryButton.textContent = "Yenileniyor...";
      waitingWorker.postMessage({ type: "SKIP_WAITING" });
      return;
    }

    if (panelMode !== "install" || !deferredInstallPrompt) {
      return;
    }

    const prompt = deferredInstallPrompt;
    deferredInstallPrompt = null;
    await prompt.prompt();
    const choice = await prompt.userChoice;

    if (choice.outcome !== "accepted") {
      rememberDismissal("install");
    }
    hidePanel();
  });

  window.addEventListener("beforeinstallprompt", (event) => {
    event.preventDefault();
    deferredInstallPrompt = event;
    showPanel("install");
  });

  window.addEventListener("appinstalled", () => {
    deferredInstallPrompt = null;
    hidePanel();
  });

  navigator.serviceWorker.addEventListener("controllerchange", () => {
    if (refreshing) {
      return;
    }

    refreshing = true;
    window.location.reload();
  });

  const trackWorker = (worker) => {
    if (!worker) {
      return;
    }

    worker.addEventListener("statechange", () => {
      if (worker.state === "installed" && navigator.serviceWorker.controller) {
        waitingWorker = worker;
        showPanel("update");
      }
    });
  };

  window.addEventListener("load", async () => {
    try {
      const registration = await navigator.serviceWorker.register(
        "/service-worker.js",
        { scope: "/", updateViaCache: "none" }
      );

      if (registration.waiting && navigator.serviceWorker.controller) {
        waitingWorker = registration.waiting;
        showPanel("update");
      }

      registration.addEventListener("updatefound", () => {
        trackWorker(registration.installing);
      });

      if (ios && !standalone && !deferredInstallPrompt) {
        showPanel("ios");
      }
    } catch (error) {
      console.warn("Uygulama kurulumu hazırlanamadı.", error);
    }
  });
})();

(() => {
  "use strict";

  const backTarget = document.body.dataset.swipeBackTarget;
  const indicator = document.querySelector("[data-edge-swipe-indicator]");

  if (!backTarget || !indicator) {
    return;
  }

  const edgeStart = 34;
  const maxVerticalDrift = 72;
  const emptyPointer = () => ({
    id: null,
    startX: 0,
    startY: 0,
    tracking: false
  });
  let pointer = emptyPointer();
  let suppressClick = false;
  let suppressClickTimer = 0;

  const activationDistance = () => Math.min(
    60,
    Math.max(44, window.innerWidth * 0.125)
  );

  const updateIndicator = (distanceX, ready) => {
    const progress = Math.min(
      Math.max(distanceX / activationDistance(), 0),
      1
    );

    indicator.style.setProperty("--edge-swipe-progress", String(progress));
    indicator.classList.toggle("ready", ready);
  };

  const clearPointer = () => {
    pointer = emptyPointer();
    document.body.classList.remove("edge-swipe-active");
    updateIndicator(0, false);
  };

  window.addEventListener("pointerdown", (event) => {
    const validMouseButton = event.pointerType !== "mouse"
      || (event.button === 0 && event.buttons === 1);

    if (!event.isPrimary || !validMouseButton || event.clientX > edgeStart) {
      return;
    }

    pointer = {
      id: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      tracking: true
    };
    updateIndicator(0, false);
  }, true);

  window.addEventListener("pointermove", (event) => {
    if (!pointer.tracking || event.pointerId !== pointer.id) {
      return;
    }

    const distanceX = event.clientX - pointer.startX;
    const distanceY = Math.abs(event.clientY - pointer.startY);

    if (distanceY > maxVerticalDrift && distanceY > Math.abs(distanceX)) {
      clearPointer();
      return;
    }

    if (distanceX > 8 && distanceX > distanceY) {
      event.preventDefault();
      document.body.classList.add("edge-swipe-active");
    }

    const ready = distanceX >= activationDistance()
      && distanceY <= maxVerticalDrift
      && distanceX > distanceY * 1.25;
    updateIndicator(distanceX, ready);
  }, { capture: true, passive: false });

  window.addEventListener("pointerup", (event) => {
    if (!pointer.tracking || event.pointerId !== pointer.id) {
      return;
    }

    const distanceX = event.clientX - pointer.startX;
    const distanceY = Math.abs(event.clientY - pointer.startY);
    const shouldNavigate = distanceX >= activationDistance()
      && distanceY <= maxVerticalDrift
      && distanceX > distanceY * 1.25;

    if (shouldNavigate) {
      event.preventDefault();
      suppressClick = true;
      window.clearTimeout(suppressClickTimer);
      suppressClickTimer = window.setTimeout(() => {
        suppressClick = false;
      }, 350);
    }

    clearPointer();

    if (shouldNavigate) {
      window.location.replace(backTarget);
    }
  }, true);

  window.addEventListener("pointercancel", clearPointer, true);
  window.addEventListener("blur", clearPointer);
  window.addEventListener("click", (event) => {
    if (!suppressClick) {
      return;
    }

    suppressClick = false;
    window.clearTimeout(suppressClickTimer);
    event.preventDefault();
    event.stopPropagation();
  }, true);
})();
