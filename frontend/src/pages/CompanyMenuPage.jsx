import React from 'react';
import { Link } from 'react-router-dom';
import HeaderBranding from '../components/HeaderBranding';
import './MenuPage.css';

function CompanyMenuPage() {
  return (
    <main className="menu-page">
      <div className="menu-page-inner">
        <header className="menu-identity company-menu-identity">
          <div className="company-menu-heading">
            <span className="menu-identity-icon" aria-hidden="true">🏢</span>
            <div>
              <p>GAZETE FİRMASI</p>
              <h1>FİRMA MENÜSÜ</h1>
            </div>
          </div>
          <HeaderBranding variant="corporate" />
        </header>

        <nav className="menu-tile-grid company-menu-grid" aria-label="Gazete firması menüsü">
          <Link className="menu-tile" to="/distributors">
            <span className="menu-tile-icon" aria-hidden="true">◉</span>
            <strong>DAĞITICILAR</strong>
            <small>Dağıtıcı kayıtları ve ödeme planları</small>
          </Link>
          <Link className="menu-tile" to="/menu/company/settings">
            <span className="menu-tile-icon" aria-hidden="true">⚙️</span>
            <strong>FİRMA AYARLARI</strong>
            <small>Firma logosu ve dağıtıcı profil görseli</small>
          </Link>
        </nav>
      </div>
    </main>
  );
}

export default CompanyMenuPage;
