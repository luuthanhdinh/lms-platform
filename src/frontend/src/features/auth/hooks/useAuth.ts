import keycloak from '@/lib/keycloak'

export function useAuth() {
  return {
    isAuthenticated: keycloak.authenticated ?? false,
    token: keycloak.token,
    userId: keycloak.tokenParsed?.sub as string,
    tenantId: keycloak.tokenParsed?.['tenant_id'] as string,
    roles: (keycloak.tokenParsed?.realm_access?.roles ?? []) as string[],
    hasRole: (role: string) => keycloak.hasRealmRole(role),
    login: () => keycloak.login({ redirectUri: window.location.origin + window.location.pathname }),
    logout: () => keycloak.logout({ redirectUri: window.location.origin }),
  }
}
