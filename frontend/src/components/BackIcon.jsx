import React from 'react';

function BackIcon({ className = '' }) {
  return (
    <svg
      className={className}
      viewBox="0 0 36 36"
      aria-hidden="true"
      focusable="false"
    >
      <path
        d="M15.5 2 2.5 11l13 9.5v-6h2.8"
        fill="currentColor"
        stroke="currentColor"
        strokeLinejoin="round"
        strokeWidth="1.5"
      />
      <path
        d="M16.5 11.5h2.2a11.8 11.8 0 1 1-11.4 14.9"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeWidth="5.4"
      />
    </svg>
  );
}

export default BackIcon;
