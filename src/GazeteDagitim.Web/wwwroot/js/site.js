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
