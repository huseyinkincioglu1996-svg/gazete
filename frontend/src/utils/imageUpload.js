const ACCEPTED_IMAGE_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);
const MAX_SOURCE_BYTES = 10 * 1024 * 1024;
const MAX_OUTPUT_BYTES = 2 * 1024 * 1024;
const MAX_IMAGE_EDGE = 640;

const getDataUrlByteLength = (dataUrl) => {
  const encoded = String(dataUrl).split(',')[1] || '';
  const padding = encoded.endsWith('==') ? 2 : encoded.endsWith('=') ? 1 : 0;
  return Math.floor((encoded.length * 3) / 4) - padding;
};

const loadImage = (source) => new Promise((resolve, reject) => {
  const image = new Image();
  image.onload = () => resolve(image);
  image.onerror = () => reject(new Error('Görsel dosyası okunamadı.'));
  image.src = source;
});

const readFileAsDataUrl = (file) => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result);
  reader.onerror = () => reject(new Error('Görsel dosyası okunamadı.'));
  reader.readAsDataURL(file);
});

export async function prepareImageDataUrl(file) {
  if (!file) {
    throw new Error('Lütfen bir görsel seçin.');
  }
  if (!ACCEPTED_IMAGE_TYPES.has(file.type)) {
    throw new Error('Yalnızca PNG, JPEG veya WebP görseller kullanılabilir.');
  }
  if (file.size > MAX_SOURCE_BYTES) {
    throw new Error('Seçilen görsel en fazla 10 MB olabilir.');
  }

  const source = await readFileAsDataUrl(file);
  const image = await loadImage(source);
  const scale = Math.min(1, MAX_IMAGE_EDGE / Math.max(image.naturalWidth, image.naturalHeight));
  const width = Math.max(1, Math.round(image.naturalWidth * scale));
  const height = Math.max(1, Math.round(image.naturalHeight * scale));
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext('2d');
  if (!context) {
    throw new Error('Görsel işleme başlatılamadı.');
  }
  context.drawImage(image, 0, 0, width, height);

  let quality = 0.88;
  let result = canvas.toDataURL('image/webp', quality);
  while (getDataUrlByteLength(result) > MAX_OUTPUT_BYTES && quality > 0.45) {
    quality -= 0.1;
    result = canvas.toDataURL('image/webp', quality);
  }

  if (getDataUrlByteLength(result) > MAX_OUTPUT_BYTES) {
    throw new Error('Görsel işlendikten sonra 2 MB sınırını aşıyor.');
  }

  return result;
}
