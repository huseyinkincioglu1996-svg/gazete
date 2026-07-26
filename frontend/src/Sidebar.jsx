import React from 'react';
import { NavLink } from 'react-router-dom';
import './Sidebar.css';

function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <span className="sidebar-logo-icon">📰</span>
        <span className="sidebar-logo-text">Gazete Dağıtım</span>
      </div>

      <nav className="sidebar-nav">
        <ul>
          <li>
            <NavLink to="/" end className={({ isActive }) => isActive ? 'sidebar-link active' : 'sidebar-link'}>
              <span className="sidebar-icon">📊</span>
              <span>Raporlar</span>
            </NavLink>
          </li>
          <li>
            <NavLink to="/distributors" className={({ isActive }) => isActive ? 'sidebar-link active' : 'sidebar-link'}>
              <span className="sidebar-icon">👥</span>
              <span>Dağıtıcılar</span>
            </NavLink>
          </li>
          <li>
            <NavLink to="/payments" className={({ isActive }) => isActive ? 'sidebar-link active' : 'sidebar-link'}>
              <span className="sidebar-icon">💰</span>
              <span>Ödemeler</span>
            </NavLink>
          </li>
        </ul>
      </nav>

      <div className="sidebar-footer">
        <p>© 2026 Gazete Dağıtım</p>
      </div>
    </aside>
  );
}

export default Sidebar;
