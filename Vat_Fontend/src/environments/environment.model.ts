/**
 * Enterprise VAT Management System - Environment Interface
 * Strictly typed configuration contract across Development, Staging, and Production.
 */
export interface Environment {
  /** Indicates whether the application is running in production mode */
  readonly production: boolean;

  /** Display title for the enterprise VAT application */
  readonly appName: string;

  /** Semantic version of the build */
  readonly version: string;

  /** Canonical name of the current deployment environment */
  readonly environmentName: 'development' | 'staging' | 'production';

  /** API Gateway route prefix (typically '/api/v1' when reverse-proxied) */
  readonly apiGatewayUrl: string;

  /** Base URL of the backend host or gateway (empty in prod when served under same domain) */
  readonly backendBaseUrl: string;

  /** Health check endpoint for ASP.NET Core probes */
  readonly healthCheckUrl: string;

  /** Controls whether fallback mock data is utilized if backend is offline */
  readonly useMockData: boolean;

  /** Default application language locale */
  readonly defaultLocale: 'en' | 'bn';

  /** Feature toggle matrix */
  readonly features: {
    readonly liveBackendSync: boolean;
    readonly branchIsolation: boolean;
    readonly multiRoleSupport: boolean;
    readonly offlineMode: boolean;
    readonly debugLogging: boolean;
    readonly showEnvironmentBadge: boolean;
  };

  /** Direct microservice endpoint URLs (Ports 5101-5112) for development or hybrid topologies */
  readonly services: {
    readonly identity: string;
    readonly company: string;
    readonly masterData: string;
    readonly purchase: string;
    readonly sales: string;
    readonly inventory: string;
    readonly manufacturing: string;
    readonly vatEngine: string;
    readonly compliance: string;
    readonly workflow: string;
    readonly reporting: string;
    readonly integration: string;
  };

  /** NBR Bangladesh statutory constants & validation thresholds */
  readonly nbr: {
    readonly binLength: number;
    readonly hsCodeLength: number;
    readonly standardVatRate: number;
    readonly advanceTaxRate: number;
    readonly bankingPaymentThreshold: number;
    readonly highValueTransactionThreshold: number;
    readonly defaultCurrency: string;
    readonly currencySymbol: string;
  };

  /** Authentication & Session Token Configuration */
  readonly auth: {
    readonly tokenKey: string;
    readonly refreshTokenKey: string;
    readonly tokenHeader: string;
    readonly tokenPrefix: string;
    readonly sessionTimeoutMinutes: number;
    readonly refreshTokenThresholdMinutes: number;
  };

  /** UI Pagination & Export Bounds */
  readonly pagination: {
    readonly defaultPageSize: number;
    readonly pageSizeOptions: readonly number[];
    readonly maxExportRows: number;
  };
}

