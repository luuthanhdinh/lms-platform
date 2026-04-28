import Keycloak from 'keycloak-js'
import { env } from './env'

const keycloak = new Keycloak({
  url: env.VITE_KEYCLOAK_URL,
  realm: env.VITE_KEYCLOAK_REALM,
  clientId: env.VITE_KEYCLOAK_CLIENT_ID,
})

export default keycloak
