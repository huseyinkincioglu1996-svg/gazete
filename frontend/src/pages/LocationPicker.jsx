import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import './LocationPicker.css';

const DEFAULT_MAP_CENTER = { lat: 41.0082, lng: 28.9784 };
const GOOGLE_MAPS_API_KEY = (process.env.REACT_APP_GOOGLE_MAPS_API_KEY || '').trim();

let googleMapsLoaderPromise;

const isValidCoordinatePair = (latitude, longitude) => (
  Number.isFinite(latitude)
  && Number.isFinite(longitude)
  && latitude >= -90
  && latitude <= 90
  && longitude >= -180
  && longitude <= 180
);

const normalizeCoordinates = (value) => {
  if (!value) return null;

  const latitudeValue = String(value.enlem ?? '').trim();
  const longitudeValue = String(value.boylam ?? '').trim();
  if (!latitudeValue || !longitudeValue) return null;

  const latitude = Number(latitudeValue.replace(',', '.'));
  const longitude = Number(longitudeValue.replace(',', '.'));
  return isValidCoordinatePair(latitude, longitude)
    ? { enlem: latitude, boylam: longitude }
    : null;
};

const parseCoordinatePair = (value) => {
  if (!value) return null;

  const match = String(value).match(
    /(-?\d{1,3}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)/,
  );
  if (!match) return null;

  const latitude = Number(match[1]);
  const longitude = Number(match[2]);
  return isValidCoordinatePair(latitude, longitude)
    ? { enlem: latitude, boylam: longitude }
    : null;
};

export const parseGoogleMapsCoordinates = (value) => {
  const trimmedValue = String(value || '').trim();
  if (!trimmedValue) return null;

  let decodedValue = trimmedValue;
  try {
    decodedValue = decodeURIComponent(trimmedValue);
  } catch (error) {
    // Bazı kopyalanmış bağlantılar geçersiz yüzde karakteri içerebilir.
  }

  const atMatch = decodedValue.match(
    /@(-?\d{1,3}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)(?:,|\/|$)/,
  );
  if (atMatch) {
    return parseCoordinatePair(`${atMatch[1]},${atMatch[2]}`);
  }

  try {
    const parsedUrl = new URL(trimmedValue);
    for (const parameterName of ['query', 'q']) {
      const coordinates = parseCoordinatePair(parsedUrl.searchParams.get(parameterName));
      if (coordinates) return coordinates;
    }
  } catch (error) {
    // URL değilse yalnızca koordinat biçimini denemeye devam ederiz.
  }

  return parseCoordinatePair(decodedValue);
};

export const buildGoogleMapsUrl = (coordinates, address = '') => {
  const normalizedCoordinates = normalizeCoordinates(coordinates);
  const query = normalizedCoordinates
    ? `${normalizedCoordinates.enlem},${normalizedCoordinates.boylam}`
    : String(address || '').trim();

  return query
    ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`
    : 'https://www.google.com/maps';
};

const loadGoogleMaps = (apiKey) => {
  if (window.google?.maps?.importLibrary) {
    return Promise.resolve(window.google.maps);
  }
  if (googleMapsLoaderPromise) return googleMapsLoaderPromise;

  googleMapsLoaderPromise = new Promise((resolve, reject) => {
    const callbackName = `__gazeteGoogleMapsReady_${Date.now()}`;
    const script = document.createElement('script');

    window[callbackName] = () => {
      delete window[callbackName];
      resolve(window.google.maps);
    };

    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&v=weekly&libraries=marker&loading=async&callback=${callbackName}`;
    script.async = true;
    script.defer = true;
    script.dataset.gazeteGoogleMaps = 'true';
    script.onerror = () => {
      delete window[callbackName];
      googleMapsLoaderPromise = undefined;
      reject(new Error('Google Haritalar yüklenemedi.'));
    };
    document.head.appendChild(script);
  });

  return googleMapsLoaderPromise;
};

const roundCoordinate = (value) => Number(Number(value).toFixed(7));

function LocationPicker({ value, address, onChange }) {
  const [mapsUrl, setMapsUrl] = useState('');
  const [locating, setLocating] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const [mapStatus, setMapStatus] = useState(GOOGLE_MAPS_API_KEY ? 'loading' : 'unavailable');
  const mapContainerRef = useRef(null);
  const mapRef = useRef(null);
  const markerRef = useRef(null);
  const mapListenersRef = useRef([]);
  const onChangeRef = useRef(onChange);
  const valueRef = useRef(value);

  const validCoordinates = useMemo(() => normalizeCoordinates(value), [value]);
  const mapsHref = buildGoogleMapsUrl(validCoordinates, address);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    valueRef.current = value;

    if (!mapRef.current || !markerRef.current) return;
    const coordinates = normalizeCoordinates(value);
    if (!coordinates) {
      markerRef.current.map = null;
      return;
    }

    const position = { lat: coordinates.enlem, lng: coordinates.boylam };
    markerRef.current.map = mapRef.current;
    markerRef.current.position = position;
    mapRef.current.panTo(position);
  }, [value]);

  const applyCoordinates = useCallback((coordinates, message) => {
    const nextCoordinates = {
      enlem: roundCoordinate(coordinates.enlem),
      boylam: roundCoordinate(coordinates.boylam),
    };
    onChangeRef.current(nextCoordinates);
    setFeedback({ type: 'success', text: message });
  }, []);

  useEffect(() => {
    if (!GOOGLE_MAPS_API_KEY || !mapContainerRef.current) return undefined;

    let cancelled = false;

    const initializeMap = async () => {
      try {
        const maps = await loadGoogleMaps(GOOGLE_MAPS_API_KEY);
        const [{ Map }, { AdvancedMarkerElement }] = await Promise.all([
          maps.importLibrary('maps'),
          maps.importLibrary('marker'),
        ]);
        if (cancelled || !mapContainerRef.current) return;

        const savedCoordinates = normalizeCoordinates(valueRef.current);
        const center = savedCoordinates
          ? { lat: savedCoordinates.enlem, lng: savedCoordinates.boylam }
          : DEFAULT_MAP_CENTER;
        const map = new Map(mapContainerRef.current, {
          center,
          zoom: savedCoordinates ? 17 : 11,
          mapId: 'DEMO_MAP_ID',
          streetViewControl: false,
          mapTypeControl: false,
          fullscreenControl: true,
        });
        const marker = new AdvancedMarkerElement({
          map: savedCoordinates ? map : null,
          position: center,
          gmpDraggable: true,
          title: 'Teslimat konumu',
        });

        const updateFromMap = (event, message) => {
          if (!event.latLng) return;
          const coordinates = {
            enlem: event.latLng.lat(),
            boylam: event.latLng.lng(),
          };
          marker.map = map;
          marker.position = { lat: coordinates.enlem, lng: coordinates.boylam };
          applyCoordinates(coordinates, message);
        };

        mapListenersRef.current = [
          map.addListener('click', (event) => {
            updateFromMap(event, 'Haritada seçtiğiniz nokta teslimat konumu olarak ayarlandı.');
          }),
          marker.addListener('dragend', (event) => {
            updateFromMap(event, 'Harita işaretçisinin yeni konumu kaydedilmeye hazır.');
          }),
        ];
        mapRef.current = map;
        markerRef.current = marker;
        setMapStatus('ready');
      } catch (error) {
        if (!cancelled) {
          setMapStatus('error');
          setFeedback({
            type: 'error',
            text: 'Gömülü harita yüklenemedi. Konumu koordinatla, bağlantıyla veya cihazınızdan belirleyebilirsiniz.',
          });
        }
      }
    };

    initializeMap();

    return () => {
      cancelled = true;
      mapListenersRef.current.forEach((listener) => listener?.remove?.());
      mapListenersRef.current = [];
      if (markerRef.current) markerRef.current.map = null;
      markerRef.current = null;
      mapRef.current = null;
    };
  }, [applyCoordinates]);

  const handleCoordinateChange = (fieldName, fieldValue) => {
    const nextLocation = {
      enlem: value?.enlem ?? '',
      boylam: value?.boylam ?? '',
      [fieldName]: fieldValue,
    };

    setFeedback(null);
    onChange(
      String(nextLocation.enlem).trim() || String(nextLocation.boylam).trim()
        ? nextLocation
        : null,
    );
  };

  const handleUseCurrentLocation = () => {
    setFeedback(null);
    if (!navigator.geolocation) {
      setFeedback({
        type: 'error',
        text: 'Bu cihaz konum bilgisini desteklemiyor. Koordinatları elle girebilirsiniz.',
      });
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocating(false);
        applyCoordinates(
          {
            enlem: position.coords.latitude,
            boylam: position.coords.longitude,
          },
          'Cihazınızın mevcut konumu teslimat noktası olarak ayarlandı.',
        );
      },
      (error) => {
        setLocating(false);
        const permissionDenied = error.code === error.PERMISSION_DENIED;
        setFeedback({
          type: 'error',
          text: permissionDenied
            ? 'Konum izni verilmedi. Tarayıcı iznini açabilir veya koordinatları elle girebilirsiniz.'
            : 'Mevcut konum alınamadı. Açık alanda tekrar deneyebilir veya koordinatları elle girebilirsiniz.',
        });
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
    );
  };

  const handleParseMapsUrl = () => {
    const coordinates = parseGoogleMapsCoordinates(mapsUrl);
    if (!coordinates) {
      setFeedback({
        type: 'error',
        text: 'Bağlantıda koordinat bulunamadı. Google Maps uzun bağlantısını (@enlem,boylam veya query/q) yapıştırın.',
      });
      return;
    }

    applyCoordinates(
      coordinates,
      'Google Maps bağlantısındaki konum teslimat noktası olarak ayarlandı.',
    );
  };

  const handleClearLocation = () => {
    setMapsUrl('');
    setFeedback({ type: 'success', text: 'Kayıtlı teslimat konumu temizlendi.' });
    onChange(null);
  };

  return (
    <section className="location-picker" aria-labelledby="subscriber-location-title">
      <div className="location-picker-heading">
        <div>
          <h3 id="subscriber-location-title">Teslimat konumu</h3>
          <p>Kapı girişini hassas biçimde belirlemek için haritadan seçin veya koordinat girin.</p>
        </div>
        {validCoordinates && (
          <span className="location-picker-selected">Konum belirlendi</span>
        )}
      </div>

      {GOOGLE_MAPS_API_KEY ? (
        <div className="location-map-shell">
          <div
            ref={mapContainerRef}
            className="location-map"
            role="application"
            aria-label="Teslimat konumunu seçmek için Google Haritası"
          />
          {mapStatus === 'loading' && (
            <div className="location-map-overlay" role="status">Harita yükleniyor…</div>
          )}
          {mapStatus === 'error' && (
            <div className="location-map-overlay location-map-overlay-error">
              Harita kullanılamıyor
            </div>
          )}
        </div>
      ) : (
        <div className="location-picker-info">
          <strong>Harita içi seçim henüz etkin değil.</strong>
          <span>
            Google Maps API anahtarı ayarlandığında haritada dokunarak seçim açılır.
            Şimdi mevcut konumu, koordinatları veya Google Maps bağlantısını kullanabilirsiniz.
          </span>
        </div>
      )}

      <div className="location-coordinate-grid">
        <div className="location-coordinate-field">
          <label htmlFor="subscriber-location-latitude">Enlem</label>
          <input
            id="subscriber-location-latitude"
            type="number"
            inputMode="decimal"
            min="-90"
            max="90"
            step="any"
            value={value?.enlem ?? ''}
            onChange={(event) => handleCoordinateChange('enlem', event.target.value)}
            placeholder="41.0082000"
          />
        </div>
        <div className="location-coordinate-field">
          <label htmlFor="subscriber-location-longitude">Boylam</label>
          <input
            id="subscriber-location-longitude"
            type="number"
            inputMode="decimal"
            min="-180"
            max="180"
            step="any"
            value={value?.boylam ?? ''}
            onChange={(event) => handleCoordinateChange('boylam', event.target.value)}
            placeholder="28.9784000"
          />
        </div>
        <button
          type="button"
          className="location-current-button"
          onClick={handleUseCurrentLocation}
          disabled={locating}
        >
          {locating ? 'Konum alınıyor…' : 'Konumumu kullan'}
        </button>
      </div>

      <div className="location-link-row">
        <div className="location-link-field">
          <label htmlFor="subscriber-google-maps-url">Google Maps uzun bağlantısı</label>
          <input
            id="subscriber-google-maps-url"
            type="url"
            inputMode="url"
            value={mapsUrl}
            onChange={(event) => setMapsUrl(event.target.value)}
            placeholder="https://www.google.com/maps/.../@41.0082,28.9784,..."
          />
        </div>
        <button
          type="button"
          className="location-link-apply-button"
          onClick={handleParseMapsUrl}
          disabled={!mapsUrl.trim()}
        >
          Bağlantıdan al
        </button>
      </div>

      <div className="location-picker-actions">
        <a href={mapsHref} target="_blank" rel="noreferrer">
          {validCoordinates ? 'Konumu Google Haritalar’da aç' : 'Adresi Google Haritalar’da aç'}
        </a>
        {value && (
          <button type="button" onClick={handleClearLocation}>
            Konumu temizle
          </button>
        )}
      </div>

      {feedback && (
        <p
          className={`location-picker-feedback ${feedback.type}`}
          role={feedback.type === 'error' ? 'alert' : 'status'}
        >
          {feedback.text}
        </p>
      )}
    </section>
  );
}

export default LocationPicker;
