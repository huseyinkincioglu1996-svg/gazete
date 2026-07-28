import React, { useCallback, useEffect, useMemo, useState } from 'react';
import api from '../api';
import { useBranding } from '../BrandingContext';
import { prepareImageDataUrl } from '../utils/imageUpload';
import './CompanySettings.css';

const getErrorMessage = (error, fallback) =>
  error.response?.data?.hata
  || error.response?.data?.mesaj
  || error.response?.data?.message
  || error.message
  || fallback;

const normalizeDistributors = (data) => (Array.isArray(data) ? data : [])
  .map((distributor) => ({
    ...distributor,
    _id: String(distributor._id),
    profil_gorseli: typeof distributor.profil_gorseli === 'string'
      ? distributor.profil_gorseli
      : null,
  }));

function CompanySettings() {
  const { refreshBranding } = useBranding();
  const [distributors, setDistributors] = useState([]);
  const [companyLogo, setCompanyLogo] = useState(null);
  const [selectedDistributorId, setSelectedDistributorId] = useState('');
  const [profileImage, setProfileImage] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [processingLogo, setProcessingLogo] = useState(false);
  const [processingProfile, setProcessingProfile] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const selectedDistributor = useMemo(
    () => distributors.find((item) => item._id === selectedDistributorId) || null,
    [distributors, selectedDistributorId],
  );

  const fetchSettings = useCallback(async () => {
    setLoading(true);
    setLoadFailed(false);
    setError('');

    try {
      const [settingsResponse, distributorsResponse] = await Promise.all([
        api.get('/api/company-settings'),
        api.get('/api/distributors?includeInactive=true'),
      ]);
      const nextDistributors = normalizeDistributors(distributorsResponse.data);
      const configuredId = settingsResponse.data?.vitrin_dagitici_id
        ? String(settingsResponse.data.vitrin_dagitici_id)
        : '';
      const fallbackId = nextDistributors.find((item) => item.aktif !== false)?._id
        || nextDistributors[0]?._id
        || '';
      const nextSelectedId = nextDistributors.some((item) => item._id === configuredId)
        ? configuredId
        : fallbackId;
      const nextSelectedDistributor = nextDistributors.find(
        (item) => item._id === nextSelectedId,
      );

      setDistributors(nextDistributors);
      setCompanyLogo(
        typeof settingsResponse.data?.firma_logosu === 'string'
          ? settingsResponse.data.firma_logosu
          : null,
      );
      setSelectedDistributorId(nextSelectedId);
      setProfileImage(nextSelectedDistributor?.profil_gorseli || null);
    } catch (requestError) {
      setLoadFailed(true);
      setError(getErrorMessage(requestError, 'Firma ayarları yüklenemedi.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const handleDistributorChange = (event) => {
    const distributorId = event.target.value;
    const distributor = distributors.find((item) => item._id === distributorId);
    setSelectedDistributorId(distributorId);
    setProfileImage(distributor?.profil_gorseli || null);
    setNotice('');
  };

  const handleImageSelection = async (event, type) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    setError('');
    setNotice('');
    const setProcessing = type === 'logo' ? setProcessingLogo : setProcessingProfile;
    setProcessing(true);
    try {
      const dataUrl = await prepareImageDataUrl(file);
      if (type === 'logo') {
        setCompanyLogo(dataUrl);
      } else {
        setProfileImage(dataUrl);
      }
    } catch (imageError) {
      setError(getErrorMessage(imageError, 'Görsel hazırlanamadı.'));
    } finally {
      setProcessing(false);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setNotice('');

    try {
      let updatedDistributor = null;
      if (selectedDistributorId) {
        const response = await api.put(`/api/distributors/${selectedDistributorId}`, {
          profil_gorseli: profileImage,
        });
        updatedDistributor = {
          ...response.data,
          _id: String(response.data._id),
          profil_gorseli: typeof response.data.profil_gorseli === 'string'
            ? response.data.profil_gorseli
            : null,
        };
      }

      await api.put('/api/company-settings', {
        firma_logosu: companyLogo,
        vitrin_dagitici_id: selectedDistributorId || null,
      });

      if (updatedDistributor) {
        setDistributors((current) => current.map((item) => (
          item._id === updatedDistributor._id ? updatedDistributor : item
        )));
      }
      await refreshBranding();
      setNotice('Firma logosu ve dağıtıcı profil bilgileri kaydedildi.');
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Firma ayarları kaydedilemedi.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="company-settings-page">
      <header className="company-settings-heading">
        <div>
          <h1>Firma Ayarları</h1>
          <p>Firma logosunu ve sağ üstte gösterilecek dağıtıcı profilini belirleyin.</p>
        </div>
      </header>

      {error && (
        <div className="company-settings-feedback error" role="alert">
          <span>{error}</span>
          {loadFailed && (
            <button type="button" onClick={fetchSettings} disabled={loading}>
              Tekrar dene
            </button>
          )}
        </div>
      )}
      {notice && (
        <div className="company-settings-feedback success" role="status">
          {notice}
        </div>
      )}

      <form className="company-settings-form" onSubmit={handleSubmit} aria-busy={loading}>
        <div className="company-settings-grid">
          <section className="branding-setting-card" aria-labelledby="company-logo-title">
            <div className="branding-setting-card-heading">
              <div>
                <h2 id="company-logo-title">Firma logosu</h2>
                <p>Logo, uygulamanın sağ üst köşesinde gösterilir.</p>
              </div>
              <span className="branding-setting-badge">FİRMA</span>
            </div>

            <div className="branding-preview logo">
              {companyLogo ? (
                <img src={companyLogo} alt="Firma logosu önizlemesi" />
              ) : (
                <span aria-hidden="true">📰</span>
              )}
            </div>

            <div className="branding-file-actions">
              <input
                id="company-logo-file"
                className="branding-file-input"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                disabled={loadFailed || loading || saving || processingLogo}
                onChange={(event) => handleImageSelection(event, 'logo')}
                aria-describedby="company-logo-help"
              />
              <label
                className={`branding-file-button${
                  loadFailed || loading || saving || processingLogo ? ' disabled' : ''
                }`}
                htmlFor="company-logo-file"
              >
                {processingLogo ? 'Hazırlanıyor…' : 'Logo seç'}
              </label>
              {companyLogo && (
                <button
                  type="button"
                  className="branding-remove-button"
                  onClick={() => setCompanyLogo(null)}
                  disabled={saving}
                >
                  Logoyu kaldır
                </button>
              )}
            </div>
            <small id="company-logo-help" className="branding-help">
              PNG, JPEG veya WebP. Görsel otomatik olarak uygun boyuta küçültülür.
            </small>
          </section>

          <section className="branding-setting-card" aria-labelledby="profile-image-title">
            <div className="branding-setting-card-heading">
              <div>
                <h2 id="profile-image-title">Dağıtıcı profili</h2>
                <p>Seçilen dağıtıcı ve profil görseli sağ üstte gösterilir.</p>
              </div>
              <span className="branding-setting-badge distributor">DAĞITICI</span>
            </div>

            <div className="branding-field">
              <label htmlFor="branding-distributor">Gösterilecek dağıtıcı</label>
              <select
                id="branding-distributor"
                value={selectedDistributorId}
                onChange={handleDistributorChange}
                disabled={loading || saving || distributors.length === 0}
              >
                {distributors.length === 0 ? (
                  <option value="">Dağıtıcı bulunamadı</option>
                ) : (
                  distributors.map((distributor) => (
                    <option key={distributor._id} value={distributor._id}>
                      {distributor.isim}{distributor.aktif === false ? ' (Pasif)' : ''}
                    </option>
                  ))
                )}
              </select>
            </div>

            <div className="branding-preview avatar">
              {profileImage ? (
                <img
                  src={profileImage}
                  alt={`${selectedDistributor?.isim || 'Dağıtıcı'} profil görseli önizlemesi`}
                />
              ) : (
                <span aria-hidden="true">👤</span>
              )}
            </div>

            <div className="branding-file-actions">
              <input
                id="distributor-profile-file"
                className="branding-file-input"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                disabled={
                  loadFailed
                  || !selectedDistributorId
                  || loading
                  || saving
                  || processingProfile
                }
                onChange={(event) => handleImageSelection(event, 'profile')}
                aria-describedby="distributor-profile-help"
              />
              <label
                className={`branding-file-button${
                  !selectedDistributorId
                  || loadFailed
                  || loading
                  || saving
                  || processingProfile
                    ? ' disabled'
                    : ''
                }`}
                htmlFor="distributor-profile-file"
              >
                {processingProfile ? 'Hazırlanıyor…' : 'Profil görseli seç'}
              </label>
              {profileImage && (
                <button
                  type="button"
                  className="branding-remove-button"
                  onClick={() => setProfileImage(null)}
                  disabled={saving}
                >
                  Görseli kaldır
                </button>
              )}
            </div>
            <small id="distributor-profile-help" className="branding-help">
              Profil görseli seçilen dağıtıcı kaydına kalıcı olarak bağlanır.
            </small>
          </section>
        </div>

        <div className="company-settings-actions">
          <p>
            Kaydettiğiniz bilgiler menülerin ve işlem ekranlarının sağ üstünde görünür.
          </p>
          <button
            type="submit"
            disabled={
              loading
              || loadFailed
              || saving
              || processingLogo
              || processingProfile
            }
          >
            {saving ? 'Kaydediliyor…' : 'Firma bilgilerini kaydet'}
          </button>
        </div>
      </form>
    </div>
  );
}

export default CompanySettings;
