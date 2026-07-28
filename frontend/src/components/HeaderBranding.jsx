import React from 'react';
import { useBranding } from '../BrandingContext';
import './HeaderBranding.css';

const getInitials = (name) => String(name || '')
  .trim()
  .split(/\s+/)
  .filter(Boolean)
  .slice(0, 2)
  .map((part) => part[0]?.toLocaleUpperCase('tr-TR') || '')
  .join('') || 'D';

function HeaderBranding() {
  const { branding } = useBranding();
  const distributor = branding.vitrin_dagitici;

  return (
    <div className="header-branding" role="group" aria-label="Firma ve dağıtıcı kimliği">
      <div className="header-branding-unit" role="group" aria-label="Firma logosu">
        <span className="header-branding-image logo">
          {branding.firma_logosu ? (
            <img src={branding.firma_logosu} alt="" />
          ) : (
            <span aria-hidden="true">📰</span>
          )}
        </span>
        <span className="header-branding-copy">
          <small>FİRMA</small>
          <strong>Gazete Dağıtım</strong>
        </span>
      </div>

      {distributor && (
        <div
          className="header-branding-unit distributor"
          role="group"
          aria-label={`Dağıtıcı: ${distributor.isim}`}
        >
          <span className="header-branding-image avatar">
            {distributor.profil_gorseli ? (
              <img src={distributor.profil_gorseli} alt="" />
            ) : (
              <span aria-hidden="true">{getInitials(distributor.isim)}</span>
            )}
          </span>
          <span className="header-branding-copy">
            <small>DAĞITICI</small>
            <strong>{distributor.isim}</strong>
          </span>
        </div>
      )}
    </div>
  );
}

export default HeaderBranding;
