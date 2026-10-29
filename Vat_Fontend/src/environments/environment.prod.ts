import { Environment } from './environment.model';

export const environment: Environment = {
  production: true,
  appName: 'Enterprise VAT Management System',
  version: '1.0.0',
  environmentName: 'production',

  // Production API Gateway (Relative URL or Reverse-Proxy Gateway)
  backendBaseUrl: '',
  apiGatewayUrl: '/api/v1',
  healthCheckUrl: '/health',
  useMockData: false, // Production strictly disables all mocks
  defaultLocale: 'en',

  // Production Feature Flags
  features: {
    liveBackendSync: true,
    branchIsolation: true,
    multiRoleSupport: true,
    offlineMode: false,
    debugLogging: false,
    showEnvironmentBadge: false
  },

  // Service Endpoints (Resolved via Production API Gateway / Ingress routes)
  services: {
    identity: '/api/v1/auth',
    company: '/api/v1/companies',
    masterData: '/api/v1/masterdata',
    purchase: '/api/v1/purchases',
    sales: '/api/v1/sales',
    inventory: '/api/v1/inventory',
    manufacturing: '/api/v1/manufacturing',
    vatEngine: '/api/v1/tax',
    compliance: '/api/v1/compliance',
    workflow: '/api/v1/workflows',
    reporting: '/api/v1/reports',
    integration: '/api/v1/integration'
  },

  // National Board of Revenue (NBR) Statutory Rules
  nbr: {
    binLength: 13,
    hsCodeLength: 8,
    standardVatRate: 0.15,
    advanceTaxRate: 0.05,
    bankingPaymentThreshold: 100000, // Section 46(1)(e): BDT 1 Lakh
    highValueTransactionThreshold: 200000, // Section 51 Mushak 6.10: BDT 2 Lakh
    defaultCurrency: 'BDT',
    currencySymbol: '৳'
  },

  // Authentication & Security
  auth: {
    tokenKey: 'vat_auth_token',
    refreshTokenKey: 'vat_refresh_token',
    tokenHeader: 'Authorization',
    tokenPrefix: 'Bearer ',
    sessionTimeoutMinutes: 60,
    refreshTokenThresholdMinutes: 5
  },

  // Pagination & Display Settings
  pagination: {
    defaultPageSize: 20,
    pageSizeOptions: [10, 20, 50, 100] as const,
    maxExportRows: 100000
  }
};
