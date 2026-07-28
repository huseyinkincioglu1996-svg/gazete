const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
const MAX_IMAGE_DATA_URL_LENGTH = 23 + (4 * Math.ceil(MAX_IMAGE_BYTES / 3));
const IMAGE_DATA_URL_PATTERN =
  /^data:(image\/(?:png|jpeg|webp));base64,([A-Za-z0-9+/]+={0,2})$/;

function hasImageSignature(mimeType, buffer) {
  if (mimeType === 'image/png') {
    return (
      buffer.length >= 8 &&
      buffer.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
    );
  }

  if (mimeType === 'image/jpeg') {
    return (
      buffer.length >= 3 &&
      buffer[0] === 0xff &&
      buffer[1] === 0xd8 &&
      buffer[2] === 0xff
    );
  }

  if (mimeType === 'image/webp') {
    return (
      buffer.length >= 12 &&
      buffer.toString('ascii', 0, 4) === 'RIFF' &&
      buffer.toString('ascii', 8, 12) === 'WEBP'
    );
  }

  return false;
}

function inspectImageDataUrl(value) {
  if (typeof value !== 'string') {
    throw new TypeError('görsel bir data URL olmalıdır');
  }

  if (value.length > MAX_IMAGE_DATA_URL_LENGTH) {
    throw new RangeError('görsel en fazla 2 MB olabilir');
  }

  const match = IMAGE_DATA_URL_PATTERN.exec(value);
  if (!match) {
    throw new TypeError('yalnızca PNG, JPEG veya WebP data URL kabul edilir');
  }

  const [, mimeType, encoded] = match;
  if (encoded.length % 4 !== 0) {
    throw new TypeError('görsel Base64 verisi geçersizdir');
  }

  const buffer = Buffer.from(encoded, 'base64');
  if (buffer.length === 0 || buffer.toString('base64') !== encoded) {
    throw new TypeError('görsel Base64 verisi geçersizdir');
  }
  if (buffer.length > MAX_IMAGE_BYTES) {
    throw new RangeError('görsel en fazla 2 MB olabilir');
  }
  if (!hasImageSignature(mimeType, buffer)) {
    throw new TypeError('görsel içeriği belirtilen dosya türüyle uyuşmuyor');
  }

  return {
    mimeType,
    byteLength: buffer.length
  };
}

function isValidImageDataUrl(value) {
  try {
    inspectImageDataUrl(value);
    return true;
  } catch {
    return false;
  }
}

module.exports = {
  MAX_IMAGE_BYTES,
  MAX_IMAGE_DATA_URL_LENGTH,
  inspectImageDataUrl,
  isValidImageDataUrl
};
