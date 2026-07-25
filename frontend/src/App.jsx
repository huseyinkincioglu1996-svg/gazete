import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import Distributors from './pages/Distributors';
import Payments from './pages/Payments';
import './App.css';

function App() {
  return (
    <Router>
      <div className="app">
        <nav className="navbar">
          <div className="nav-container">
            <h2 className="nav-logo">📰 Gazete Dağıtım Sistemi</h2>
            <ul className="nav-menu">
              <li>
                <Link to="/" className="nav-link">Dashboard</Link>
              </li>
              <li>
                <Link to="/distributors" className="nav-link">Dağıtıcılar</Link>
              </li>
              <li>
                <Link to="/payments" className="nav-link">Ödemeler</Link>
              </li>
            </ul>
          </div>
        </nav>

        <main className="main-content">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/distributors" element={<Distributors />} />
            <Route path="/payments" element={<Payments />} />
          </Routes>
        </main>

        <footer className="footer">
          <p>© 2026 Gazete Dağıtım ve Ödeme Takip Sistemi</p>
        </footer>
      </div>
    </Router>
  );
}

export default App;
