import React, { useEffect } from 'react';
import {
  BrowserRouter as Router,
  Link,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
} from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import Deliveries from './pages/Deliveries';
import Distributors from './pages/Distributors';
import Payments from './pages/Payments';
import CashHandover from './pages/CashHandover';
import Subscribers from './pages/Subscribers';
import Settings from './pages/Settings';
import MenuPage from './pages/MenuPage';
import CompanyMenuPage from './pages/CompanyMenuPage';
import CompanySettings from './pages/CompanySettings';
import BackIcon from './components/BackIcon';
import HeaderBranding from './components/HeaderBranding';
import { BrandingProvider } from './BrandingContext';
import './App.css';

function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, [pathname]);

  return null;
}

function FeatureLayout() {
  const { pathname } = useLocation();
  const isCompanySection = pathname.startsWith('/menu/company/');
  const backTarget = isCompanySection ? '/menu/company' : '/menu';
  const backLabel = isCompanySection ? 'Firma menüsüne dön' : 'Ana menüye dön';

  return (
    <div className="feature-shell">
      <a className="skip-link" href="#main-content">Ana içeriğe geç</a>
      <header className="feature-toolbar">
        <Link className="feature-menu-link" to={backTarget} aria-label={backLabel}>
          <BackIcon className="back-icon" />
        </Link>
        <HeaderBranding />
      </header>

      <div className="main-wrapper">
        <main id="main-content" className="main-content" tabIndex="-1">
          <Outlet />
        </main>
        <footer className="footer">
          <p>© 2026 Gazete Dağıtım ve Ödeme Takip Sistemi</p>
        </footer>
      </div>
    </div>
  );
}

function App() {
  return (
    <Router>
      <BrandingProvider>
        <ScrollToTop />
        <Routes>
          <Route path="/" element={<Navigate to="/menu" replace />} />
          <Route path="/menu" element={<MenuPage />} />
          <Route path="/menu/company" element={<CompanyMenuPage />} />
          <Route element={<FeatureLayout />}>
            <Route path="/reports" element={<Dashboard />} />
            <Route path="/subscribers" element={<Subscribers />} />
            <Route path="/deliveries" element={<Deliveries />} />
            <Route path="/distributors" element={<Distributors />} />
            <Route path="/payments" element={<Payments />} />
            <Route path="/cash-handover" element={<CashHandover />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="/menu/company/settings" element={<CompanySettings />} />
          </Route>
          <Route path="*" element={<Navigate to="/menu" replace />} />
        </Routes>
      </BrandingProvider>
    </Router>
  );
}

export default App;
