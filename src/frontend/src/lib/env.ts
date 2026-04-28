export const env = {
  VITE_API_BASE_URL: import.meta.env.VITE_API_BASE_URL as string,
  VITE_KEYCLOAK_URL: import.meta.env.VITE_KEYCLOAK_URL as string,
  VITE_KEYCLOAK_REALM: import.meta.env.VITE_KEYCLOAK_REALM as string,
  VITE_KEYCLOAK_CLIENT_ID: import.meta.env.VITE_KEYCLOAK_CLIENT_ID as string,
} as const
