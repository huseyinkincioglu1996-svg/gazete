import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Sidebar from './Sidebar';
import Dashboard from './pages/Dashboard';
import Distributors from './pages/Distributors';
import Payments from './pages/Payments';
import './App.css';

function App() {
  return (
    <Router>
      <div className="app">
        <Sidebar />
        <div className="main-wrapper">
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
      </div>
    </Router>
  );
}

export default App;
