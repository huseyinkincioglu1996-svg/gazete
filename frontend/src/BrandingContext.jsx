import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import api from './api';

const EMPTY_BRANDING = Object.freeze({
  firma_logosu: null,
  vitrin_dagitici_id: null,
  vitrin_dagitici: null,
});

const BrandingContext = createContext({
  branding: EMPTY_BRANDING,
  loadingBranding: false,
  refreshBranding: async () => EMPTY_BRANDING,
});

const normalizeBranding = (value) => ({
  firma_logosu: typeof value?.firma_logosu === 'string' ? value.firma_logosu : null,
  vitrin_dagitici_id: value?.vitrin_dagitici_id
    ? String(value.vitrin_dagitici_id)
    : null,
  vitrin_dagitici: value?.vitrin_dagitici
    ? {
      ...value.vitrin_dagitici,
      _id: String(value.vitrin_dagitici._id),
      profil_gorseli: typeof value.vitrin_dagitici.profil_gorseli === 'string'
        ? value.vitrin_dagitici.profil_gorseli
        : null,
    }
    : null,
});

export function BrandingProvider({ children }) {
  const [branding, setBranding] = useState(EMPTY_BRANDING);
  const [loadingBranding, setLoadingBranding] = useState(true);

  const refreshBranding = useCallback(async () => {
    try {
      const response = await api.get('/api/company-settings');
      const normalized = normalizeBranding(response.data);
      setBranding(normalized);
      return normalized;
    } finally {
      setLoadingBranding(false);
    }
  }, []);

  useEffect(() => {
    refreshBranding().catch(() => {
      setBranding(EMPTY_BRANDING);
    });
  }, [refreshBranding]);

  const value = useMemo(
    () => ({ branding, loadingBranding, refreshBranding }),
    [branding, loadingBranding, refreshBranding],
  );

  return (
    <BrandingContext.Provider value={value}>
      {children}
    </BrandingContext.Provider>
  );
}

export const useBranding = () => useContext(BrandingContext);
