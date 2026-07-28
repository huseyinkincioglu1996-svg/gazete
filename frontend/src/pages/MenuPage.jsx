import React from 'react';
import { Link } from 'react-router-dom';
import HeaderBranding from '../components/HeaderBranding';
import './MenuPage.css';

const secondaryMenuItems = [
  {
    to: '/reports',
    label: 'Raporlar',
    icon: '📊',
    description: 'Güncel sonuçları inceleyin',
  },
  {
    to: '/subscribers',
    label: 'Aboneler',
    icon: '👥',
    description: 'Abone kayıtlarını yönetin',
  },
  {
    to: '/payments',
    label: 'Ödemeler',
    icon: '₺',
    description: 'Tahsilatları takip edin',
  },
  {
    to: '/cash-handover',
    label: 'Kasa Teslimi',
    icon: '💵',
    description: 'Günlük kasayı teslim edin',
  },
  {
    to: '/settings',
    label: 'Ayarlar',
    icon: '⚙️',
    description: 'Uygulama seçeneklerini belirleyin',
  },
];

function MenuPage() {
  return (
    <main className="menu-page">
      <div className="menu-page-inner">
        <header className="menu-identity">
          <span className="menu-identity-icon" aria-hidden="true">📰</span>
          <div>
            <p>GAZETE DAĞITIM</p>
            <h1>ANA MENÜ</h1>
          </div>
          <HeaderBranding />
        </header>

        <nav className="menu-tile-grid" aria-label="Uygulama menüsü">
          <Link className="menu-tile menu-tile-featured" to="/deliveries">
            <span className="menu-tile-icon" aria-hidden="true">🗞️</span>
            <strong>DAĞITIMLAR</strong>
            <small>Günlük teslimat ve tahsilat listesi</small>
          </Link>

          {secondaryMenuItems.slice(0, 2).map((item) => (
            <Link className="menu-tile" to={item.to} key={item.to}>
              <span className="menu-tile-icon" aria-hidden="true">{item.icon}</span>
              <strong>{item.label}</strong>
              <small>{item.description}</small>
            </Link>
          ))}

          <Link className="menu-tile" to="/menu/company">
            <span className="menu-tile-icon" aria-hidden="true">🏢</span>
            <strong>GAZETE FİRMASI</strong>
            <small>Firma menüsünü aç</small>
          </Link>

          {secondaryMenuItems.slice(2).map((item) => (
            <Link className="menu-tile" to={item.to} key={item.to}>
              <span className="menu-tile-icon" aria-hidden="true">{item.icon}</span>
              <strong>{item.label}</strong>
              <small>{item.description}</small>
            </Link>
          ))}

        </nav>
      </div>
    </main>
  );
}

export default MenuPage;
