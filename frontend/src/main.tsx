import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';

import '@fontsource-variable/archivo/standard.css';
import '@fontsource-variable/public-sans';
import '@fontsource-variable/jetbrains-mono';

import './styles/tokens.css';
import './styles/base.css';
import './styles/shell.css';
import './styles/pages.css';
import './styles/auth.css';

import { router } from './routes';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
