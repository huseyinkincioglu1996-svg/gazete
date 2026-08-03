const PARENT_MENU_BY_PATH = Object.freeze({
  '/menu/company': '/menu',
  '/menu/company/settings': '/menu/company',
  '/distributors': '/menu/company',
  '/reports': '/menu',
  '/subscribers': '/menu',
  '/deliveries': '/menu',
  '/payments': '/menu',
  '/cash-handover': '/menu',
  '/settings': '/menu',
});

const normalizePathname = (pathname) => {
  const value = String(pathname || '/');

  if (value === '/') {
    return value;
  }

  return value.replace(/\/+$/, '');
};

export const getParentMenu = (pathname) => (
  PARENT_MENU_BY_PATH[normalizePathname(pathname)] || null
);
