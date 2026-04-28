import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import keycloak from '@/lib/keycloak'
import { Providers } from './providers'
import '../index.css'

keycloak
  .init({
    onLoad: 'check-sso',
    silentCheckSsoRedirectUri: window.location.origin + '/silent-check-sso.html',
    pkceMethod: 'S256',
    checkLoginIframe: false,
  })
  .then(() => {
    keycloak.onTokenExpired = () => {
      keycloak.updateToken(60).catch(() => keycloak.login())
    }

    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <Providers />
      </StrictMode>,
    )
  })
