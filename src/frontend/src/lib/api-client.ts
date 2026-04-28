import axios from 'axios'
import keycloak from './keycloak'
import { env } from './env'

// Authenticated client — attaches Bearer token on every request
export const apiClient = axios.create({
  baseURL: env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

apiClient.interceptors.request.use(async config => {
  if (keycloak.token) {
    await keycloak.updateToken(30).catch(() => keycloak.login({ redirectUri: window.location.origin + window.location.pathname }))
    config.headers.Authorization = `Bearer ${keycloak.token}`
  }
  return config
})

apiClient.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) keycloak.login({ redirectUri: window.location.origin + window.location.pathname })
    return Promise.reject(err)
  },
)

// Public client — no auth header; used only for /verify/{code}
export const publicApiClient = axios.create({
  baseURL: env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})
