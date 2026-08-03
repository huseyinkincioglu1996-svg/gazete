(() => {
  const maxBytes = 2 * 1024 * 1024;
  document.querySelectorAll("[data-image-input]").forEach((input) => {
    input.addEventListener("change", () => {
      const file = input.files?.[0];
      if (!file) return;
      if (!["image/png", "image/jpeg", "image/webp"].includes(file.type)) {
        window.alert("Yalnızca PNG, JPEG veya WebP görsel seçebilirsiniz.");
        input.value = "";
        return;
      }
      if (file.size > maxBytes) {
        window.alert("Görsel en fazla 2 MB olabilir.");
        input.value = "";
        return;
      }

      const key = input.dataset.imageInput;
      const preview = document.querySelector(`[data-image-preview="${key}"]`);
      const fallback = document.querySelector(`[data-image-fallback="${key}"]`);
      if (!preview) return;
      preview.src = URL.createObjectURL(file);
      preview.hidden = false;
      if (fallback) fallback.hidden = true;
    });
  });
})();
